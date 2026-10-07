using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

using AlephBot.Core.Personality;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// O limite de pedidos da API, pra todo mundo: cada IP tem uma cota por minuto, e toda resposta
/// conta a cota no X-RateLimit-Limit e quanto ainda sobra no X-RateLimit-Remaining. Passou,
/// 429 com Retry-After até a janela virar.
///
/// De fora (Funnel ou IP público) a cota é a de uma pessoa usando a página e a IA. De dentro
/// ela é dez vezes maior: a casa nunca chega perto, e um script em loop não derruba o bot — a
/// API mora no mesmo processo que atende o Discord.
/// </summary>
public sealed class LimiteDePedidos : IDisposable
{
    /// <summary>Por minuto, por IP de fora. A página dos docs gasta uns cinco pedidos pra abrir.</summary>
    public const int DeFora = 60;

    /// <summary>Por minuto, por IP de dentro.</summary>
    public const int DeDentro = 600;

    public const string CabeçalhoDaCota = "X-RateLimit-Limit";
    public const string CabeçalhoDaSobra = "X-RateLimit-Remaining";

    private static readonly TimeSpan Janela = TimeSpan.FromMinutes(1);

    private readonly PartitionedRateLimiter<HttpContext> _limitador;
    private readonly ConcurrentDictionary<string, long> _anotados = new();
    private readonly ILogger<LimiteDePedidos> _logger;

    public LimiteDePedidos(ILogger<LimiteDePedidos> logger)
    {
        _logger = logger;

        // janela fixa: a cota inteira volta a cada minuto. Todo pedido conta, docs e 404
        // inclusive — quem varre endereço atrás de brecha também esbarra aqui
        _limitador = PartitionedRateLimiter.Create<HttpContext, Cota>(http =>
            RateLimitPartition.GetFixedWindowLimiter(CotaDe(http), cota => new FixedWindowRateLimiterOptions
            {
                PermitLimit = cota.Pedidos,
                Window = Janela,
            }));
    }

    /// <summary>Quem divide a cota (o IP) e de quantos pedidos ela é.</summary>
    internal readonly record struct Cota(string Cliente, int Pedidos);

    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<LimiteDePedidos>();
        services.AddRateLimiter(opções => opções.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

        // o limitador mora no singleton porque o header da sobra precisa perguntar a ele
        services.AddOptions<RateLimiterOptions>().Configure<LimiteDePedidos>((opções, limite) =>
        {
            opções.GlobalLimiter = limite._limitador;
            opções.OnRejected = limite.RecusaAsync;
        });
    }

    /// <summary>
    /// Entra depois do X-Forwarded-For, que é por onde eu sei quem pediu, e antes da senha dos
    /// docs, pra que chutar senha também gaste a cota.
    /// </summary>
    public static void Use(WebApplication app)
    {
        var limite = app.Services.GetRequiredService<LimiteDePedidos>();

        app.UseRateLimiter();
        app.Use((http, next) =>
        {
            var sobra = limite._limitador.GetStatistics(http)?.CurrentAvailablePermits ?? 0;

            Escreve(http.Response.Headers, CotaDe(http), sobra);
            return next(http);
        });
    }

    internal static Cota CotaDe(HttpContext http) => new(
        ClienteDe(http.Connection.RemoteIpAddress),
        RedeDeDentro.ÉDeDentro(http) ? DeDentro : DeFora);

    /// <summary>
    /// O IP de quem pede. IPv6 conta pelo /64: é o que o provedor entrega pra uma casa só, e
    /// trocar de endereço dentro dele é de graça — contando por endereço, cada pedido viria de
    /// um IP novo e o limite não valeria nada.
    /// </summary>
    internal static string ClienteDe(IPAddress? ip)
    {
        if (ip is null)
            return "sem ip";

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
            return ip.ToString();

        Span<byte> bytes = stackalloc byte[16];
        ip.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();

        return $"{new IPAddress(bytes)}/64";
    }

    /// <summary>
    /// Anoto a recusa uma vez por janela e por IP. Quem martela leva centenas de 429 por
    /// segundo, e uma linha de log por recusa encheria o disco da VPS com a mesma frase.
    /// </summary>
    internal bool DeveAnotar(string cliente, long agoraEmMs)
    {
        var janela = (long)Janela.TotalMilliseconds;

        if (_anotados.TryGetValue(cliente, out var última) && agoraEmMs - última < janela)
            return false;

        _anotados[cliente] = agoraEmMs;

        // quem parou de martelar sai da lista; sem isto ela só cresceria
        if (_anotados.Count > 1024)
        {
            foreach (var (outro, quando) in _anotados)
            {
                if (agoraEmMs - quando >= janela)
                    _anotados.TryRemove(outro, out _);
            }
        }

        return true;
    }

    private async ValueTask RecusaAsync(OnRejectedContext contexto, CancellationToken _)
    {
        var http = contexto.HttpContext;
        var cota = CotaDe(http);

        // a janela fixa responde "a janela inteira": é o mais que alguém espera, nunca menos
        var espera = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var depois) ? depois : Janela;
        var segundos = (int)Math.Ceiling(espera.TotalSeconds);

        http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
        Escreve(http.Response.Headers, cota, sobra: 0);

        if (DeveAnotar(cota.Cliente, Environment.TickCount64))
        {
            _logger.LogWarning(
                "Pedidos demais na API vindo de {Cliente}: passou dos {Pedidos} por minuto",
                cota.Cliente,
                cota.Pedidos);
        }

        await TypedResults
            .Problem(detail: Denia.ApiPedidosDemais(segundos), statusCode: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(http);
    }

    private static void Escreve(IHeaderDictionary headers, Cota cota, long sobra)
    {
        headers[CabeçalhoDaCota] = cota.Pedidos.ToString(CultureInfo.InvariantCulture);
        headers[CabeçalhoDaSobra] = sobra.ToString(CultureInfo.InvariantCulture);
    }

    public void Dispose() => _limitador.Dispose();
}

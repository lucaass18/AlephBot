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

// A cota por IP a cada minuto, contada nos headers. De dentro ela é dez vezes maior.
public sealed class LimiteDePedidos : IDisposable
{
    // Por minuto, por IP de fora. A página dos docs gasta uns cinco pedidos pra abrir.
    public const int DeFora = 60;

    // Por minuto, por IP de dentro.
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

        // janela fixa: a cota volta a cada minuto, e todo pedido conta, docs e 404 inclusive
        _limitador = PartitionedRateLimiter.Create<HttpContext, Cota>(http =>
            RateLimitPartition.GetFixedWindowLimiter(CotaDe(http), cota => new FixedWindowRateLimiterOptions
            {
                PermitLimit = cota.Pedidos,
                Window = Janela,
            }));
    }

    // Quem divide a cota (o IP) e de quantos pedidos ela é.
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

    // Depois do X-Forwarded-For, que diz quem pediu, e antes da senha dos docs.
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

    // O IP de quem pede. IPv6 conta pelo /64: dentro dele trocar de endereço é de graça.
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

    // Anoto a recusa uma vez por janela e por IP; quem martela encheria o disco da VPS.
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

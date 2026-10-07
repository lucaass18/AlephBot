using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// As camadas pra quem vem de fora (Funnel ou IP público), por cima da chave da API: ninguém
/// embute a página em outro site, quem pede demais espera, e os docs podem pedir senha. De
/// dentro nada disso pesa — a casa não tem limite nem senha.
/// </summary>
public static class ProteçãoDeFora
{
    /// <summary>
    /// Por IP, por minuto. A página dos docs gasta uns cinco pedidos pra abrir; isto sobra
    /// pra quem usa e corta quem varre, martela ou fica chutando a senha.
    /// </summary>
    public const int PedidosPorMinuto = 60;

    public static void Configure(IServiceCollection services) =>
        services.AddRateLimiter(opções =>
        {
            opções.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opções.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RedeDeDentro.ÉDeDentro(http)
                    ? RateLimitPartition.GetNoLimiter("dentro")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        http.Connection.RemoteIpAddress?.ToString() ?? "sem ip",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = PedidosPorMinuto,
                            Window = TimeSpan.FromMinutes(1),
                        }));

            opções.OnRejected = (contexto, _) =>
            {
                var http = contexto.HttpContext;
                http.Response.Headers.RetryAfter = "60";

                http.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(ProteçãoDeFora))
                    .LogWarning("Pedidos demais na API vindo de {Ip}", http.Connection.RemoteIpAddress);

                return ValueTask.CompletedTask;
            };
        });

    public static void Use(WebApplication app, ApiConfig api)
    {
        // ninguém põe a página (e a IA) dentro de outro site, e o navegador não adivinha tipo
        app.Use((http, next) =>
        {
            var headers = http.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            return next(http);
        });

        // antes da senha de propósito: chutar senha também esbarra no limite
        app.UseRateLimiter();

        // a senha guarda a página (/api/v1/docs e o que ela carrega). O /api/docs de antes só
        // redireciona pra ela, e o documento é público como o do Registry
        if (api.DocsPassword is { } senha)
        {
            var esperada = Hash(senha);

            app.Use((http, next) =>
                !http.Request.Path.StartsWithSegments(ApiDocs.Página)
                || RedeDeDentro.ÉDeDentro(http)
                || SenhaConfere(http, esperada)
                    ? next(http)
                    : PedeSenha(http));
        }
    }

    /// <summary>O Basic do HTTP: o navegador mostra a janelinha de usuário e senha sozinho.</summary>
    private static Task PedeSenha(HttpContext http)
    {
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        http.Response.Headers.WWWAuthenticate = "Basic realm=\"AlephBot\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }

    /// <summary>
    /// O usuário tanto faz; quem decide é a senha. Comparo os hashes, como a chave da API: o
    /// tempo da comparação não conta nada pra quem está chutando.
    /// </summary>
    private static bool SenhaConfere(HttpContext http, byte[] esperada)
    {
        var cabeçalho = http.Request.Headers.Authorization.ToString();

        if (!cabeçalho.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        string credenciais;

        try
        {
            credenciais = Encoding.UTF8.GetString(Convert.FromBase64String(cabeçalho["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        var separador = credenciais.IndexOf(':');

        return separador >= 0
            && CryptographicOperations.FixedTimeEquals(Hash(credenciais[(separador + 1)..]), esperada);
    }

    private static byte[] Hash(string texto) => SHA256.HashData(Encoding.UTF8.GetBytes(texto));
}

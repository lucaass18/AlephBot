using System.Security.Cryptography;
using System.Text;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// As camadas pra quem vem de fora (Funnel ou IP público), por cima da chave da API: ninguém
/// embute a página em outro site, e os docs podem pedir senha — que de dentro nunca é pedida.
/// O limite de pedidos vale pra todo mundo e mora no <see cref="LimiteDePedidos"/>.
/// </summary>
public static class ProteçãoDeFora
{
    /// <summary>
    /// Ninguém põe a página (e a IA) dentro de outro site, e o navegador não adivinha tipo.
    /// Entra antes do limite, pra valer até no 429.
    /// </summary>
    public static void UseCabeçalhos(WebApplication app) =>
        app.Use((http, next) =>
        {
            var headers = http.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            return next(http);
        });

    /// <summary>
    /// A senha guarda a página (/api/v1/docs e o que ela carrega). O /api/docs de antes só
    /// redireciona pra ela, e o documento é público como o do Registry. Entra depois do limite
    /// de pedidos de propósito: chutar senha também gasta a cota.
    /// </summary>
    public static void UseSenhaDosDocs(WebApplication app, ApiConfig api)
    {
        if (api.DocsPassword is not { } senha)
            return;

        var esperada = Hash(senha);

        app.Use((http, next) =>
            !http.Request.Path.StartsWithSegments(ApiDocs.Página)
            || RedeDeDentro.ÉDeDentro(http)
            || SenhaConfere(http, esperada)
                ? next(http)
                : PedeSenha(http));
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

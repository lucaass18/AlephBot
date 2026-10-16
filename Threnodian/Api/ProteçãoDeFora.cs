using System.Security.Cryptography;
using System.Text;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AlephBot.Threnodian.Api;

// As camadas pra quem vem de fora, por cima da chave: sem embutir a página, e a IA só pro dono.
public static class ProteçãoDeFora
{
    // Ninguém põe a página dentro de outro site. Entra antes do limite, pra valer até no 429.
    public static void UseCabeçalhos(WebApplication app) =>
        app.Use((http, next) =>
        {
            var headers = http.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "frame-ancestors 'none'";

            // o caminho conta qual rota alguém olhava, e isso não é da conta do site de destino
            headers["Referrer-Policy"] = "no-referrer";

            return next(http);
        });

    private const string Crachá = "aleph.dono";

    // Quem está em casa, ou quem entrou de fora com a senha de dono — é quem vê a IA.
    public static bool ÉDono(HttpContext http) =>
        RedeDeDentro.ÉDeDentro(http) || http.Items[Crachá] is true;

    // A documentação é aberta a todo mundo: a senha não tranca a página, só diz quem é dono.
    public static void UseCracháDosDocs(WebApplication app, ApiConfig api)
    {
        if (api.DocsAdminPassword is null)
            return;

        app.Use((http, next) =>
        {
            // a tela de entrada cuida da própria senha, e de dentro o crachá já vem de graça
            if (!http.Request.Path.StartsWithSegments(ApiDocs.Página)
                || LoginDosDocs.ÉTelaDeEntrada(http.Request.Path)
                || RedeDeDentro.ÉDeDentro(http))
            {
                return next(http);
            }

            // o biscoito vale mesmo de castigo: quem já entrou não está chutando senha
            if (LoginDosDocs.TemCrachá(http))
            {
                http.Items[Crachá] = true;
                return next(http);
            }

            // quem manda a senha no header entra na hora, e errar ali conta igual
            if (SenhaDoBasic(http) is { } senha && LoginDosDocs.Castigo(http) is null)
            {
                if (ÉSenhaDeDono(senha, api))
                {
                    LoginDosDocs.Acertou(http);
                    http.Items[Crachá] = true;
                }
                else
                {
                    LoginDosDocs.Errou(http);
                }
            }

            // sem senha, ou com ela errada, a documentação abre do mesmo jeito: só sem a IA
            return next(http);
        });
    }

    // Se a senha é a de dono. Comparada em tempo fixo: errar não pode responder mais rápido.
    public static bool ÉSenhaDeDono(string senha, ApiConfig api) =>
        api.DocsAdminPassword is { } dono
            && CryptographicOperations.FixedTimeEquals(Hash(senha), Hash(dono));

    // A senha do header, pra quem não é navegador; o usuário tanto faz.
    private static string? SenhaDoBasic(HttpContext http)
    {
        var cabeçalho = http.Request.Headers.Authorization.ToString();

        if (!cabeçalho.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return null;

        string credenciais;

        try
        {
            credenciais = Encoding.UTF8.GetString(Convert.FromBase64String(cabeçalho["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return null;
        }

        var separador = credenciais.IndexOf(':');

        return separador < 0 ? null : credenciais[(separador + 1)..];
    }

    private static byte[] Hash(string texto) => SHA256.HashData(Encoding.UTF8.GetBytes(texto));
}

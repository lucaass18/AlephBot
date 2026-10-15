using System.Security.Cryptography;
using System.Text;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AlephBot.Threnodian.Api;

// As camadas pra quem vem de fora, por cima da chave: sem embutir a página, e docs com senha.
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

    // Quanto a página entrega a quem abriu ela.
    public enum Acesso
    {
        // A documentação inteira, sem a IA — que é paga e tem dono.
        Visitante,

        // A página como em casa, IA inclusa.
        Dono,
    }

    private const string Crachá = "aleph.acesso";

    // Quem está em casa, ou quem entrou de fora com a senha de dono — é quem vê a IA.
    public static bool ÉDono(HttpContext http) =>
        RedeDeDentro.ÉDeDentro(http) || (http.Items[Crachá] as Acesso?) == Acesso.Dono;

    // A senha guarda a página: a de dono abre tudo, a de visitante abre sem a IA.
    public static void UseSenhaDosDocs(WebApplication app, ApiConfig api)
    {
        if (api.DocsPassword is null && api.DocsAdminPassword is null)
            return;

        app.Use((http, next) =>
        {
            if (!http.Request.Path.StartsWithSegments(ApiDocs.Página)
                || LoginDosDocs.ÉTelaDeEntrada(http.Request.Path)
                || RedeDeDentro.ÉDeDentro(http))
            {
                return next(http);
            }

            // o biscoito vale mesmo de castigo: quem já entrou não está chutando senha
            if (LoginDosDocs.Crachá(http) is { } doBiscoito)
            {
                http.Items[Crachá] = doBiscoito;
                return next(http);
            }

            // quem manda a senha no header entra na hora, e errar ali conta igual
            if (SenhaDoBasic(http) is { } senha)
            {
                var castigo = LoginDosDocs.Castigo(http);

                if (castigo is null && AcessoDaSenha(senha, api) is { } doHeader)
                {
                    LoginDosDocs.Acertou(http);
                    http.Items[Crachá] = doHeader;
                    return next(http);
                }

                if (castigo is null)
                    LoginDosDocs.Errou(http);
            }

            http.Response.Redirect(LoginDosDocs.Entrar);
            return Task.CompletedTask;
        });
    }

    // Que porta esta senha abre. A de dono é conferida primeiro, se as duas forem iguais.
    public static Acesso? AcessoDaSenha(string senha, ApiConfig api)
    {
        var tentada = Hash(senha);

        if (api.DocsAdminPassword is { } dono && CryptographicOperations.FixedTimeEquals(tentada, Hash(dono)))
            return Acesso.Dono;

        if (api.DocsPassword is { } visitante && CryptographicOperations.FixedTimeEquals(tentada, Hash(visitante)))
            return Acesso.Visitante;

        return null;
    }

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

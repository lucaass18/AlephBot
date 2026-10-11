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

            // o endereço daqui não viaja junto quando a página dos docs leva pra fora: o caminho
            // conta qual versão e qual rota alguém estava olhando, e isso não é da conta do site
            // de destino
            headers["Referrer-Policy"] = "no-referrer";

            return next(http);
        });

    /// <summary>Quanto a página entrega a quem abriu ela.</summary>
    public enum Acesso
    {
        /// <summary>A documentação inteira, sem a IA — que é paga e tem dono.</summary>
        Visitante,

        /// <summary>A página como em casa, IA inclusa.</summary>
        Dono,
    }

    private const string Crachá = "aleph.acesso";

    /// <summary>
    /// Quem está em casa, ou quem entrou de fora com a senha de dono. É por isto que a página
    /// decide se mostra a IA.
    /// </summary>
    public static bool ÉDono(HttpContext http) =>
        RedeDeDentro.ÉDeDentro(http) || (http.Items[Crachá] as Acesso?) == Acesso.Dono;

    /// <summary>
    /// A senha guarda a página (/api/v1/docs e o que ela carrega). São duas: a de dono abre
    /// tudo, a de visitante abre a documentação sem a IA. O /api/docs de antes só redireciona
    /// pra cá, e o documento é público como o do Registry. Entra depois do limite de pedidos de
    /// propósito: chutar senha também gasta a cota.
    /// </summary>
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

            // quem manda a senha no header entra na hora (curl, script); o navegador traz o
            // biscoito que a tela de entrada deu
            var doHeader = SenhaDoBasic(http) is { } senha ? AcessoDaSenha(senha, api) : null;

            if ((doHeader ?? LoginDosDocs.Crachá(http)) is not { } acesso)
            {
                http.Response.Redirect(LoginDosDocs.Entrar);
                return Task.CompletedTask;
            }

            http.Items[Crachá] = acesso;
            return next(http);
        });
    }

    /// <summary>
    /// Que porta esta senha abre — nenhuma, se não for uma das duas. A de dono é conferida
    /// primeiro: se as duas forem iguais por descuido, quem configurou leva o acesso maior.
    /// </summary>
    public static Acesso? AcessoDaSenha(string senha, ApiConfig api)
    {
        var tentada = Hash(senha);

        if (api.DocsAdminPassword is { } dono && CryptographicOperations.FixedTimeEquals(tentada, Hash(dono)))
            return Acesso.Dono;

        if (api.DocsPassword is { } visitante && CryptographicOperations.FixedTimeEquals(tentada, Hash(visitante)))
            return Acesso.Visitante;

        return null;
    }

    /// <summary>
    /// A senha que veio no header, pra quem não é navegador. O usuário tanto faz: quem diz quem
    /// é a pessoa é a senha.
    /// </summary>
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

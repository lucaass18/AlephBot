using System.Text.RegularExpressions;

using AlephBot.Core.Personality;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AlephBot.Threnodian.Api;

// A porta do documento pra quem vem de fora: sem entrar com a API_KEY, o openapi.json não abre.
// De dentro ele segue aberto. O biscoito morre junto com o contêiner.
public static partial class LoginDoDocumento
{
    public const string Entrar = "/api/docs";
    public const string Sair = "/api/docs/sair";

    private const string Biscoito = "aleph_docs";
    private const string Propósito = "AlephBot.Docs.v1";
    private const string Campo = "chave";

    // o biscoito vale pra tudo debaixo de /api, que é onde mora o documento de cada versão
    private const string CaminhoDoBiscoito = "/api";

    private static readonly TimeSpan Validade = TimeSpan.FromDays(30);

    private static readonly string Documento = $"/api/v{AlephApi.VersãoAtual}/openapi.json";

    // /api/openapi.json e o de cada versão: /api/v1/openapi.json, /api/v2/openapi.json
    [GeneratedRegex(@"^/api/(?:v[^/]+/)?openapi\.json$", RegexOptions.IgnoreCase)]
    private static partial Regex CaminhoDoDocumento();

    // Entra depois do limite de pedidos: chutar chave aqui gasta a mesma cota.
    public static void Use(WebApplication app) =>
        app.Use((http, next) =>
        {
            if (!CaminhoDoDocumento().IsMatch(http.Request.Path.Value ?? "") || PodeLer(http))
                return next(http);

            // navegador vai pra tela de entrar; programa leva o 401 de sempre
            if (QuerPágina(http))
            {
                http.Response.Redirect(Entrar);
                return Task.CompletedTask;
            }

            return Results.Problem(
                    detail: Denia.ApiDocumentoFechado(ApiKeyFilter.Header, Entrar),
                    statusCode: StatusCodes.Status401Unauthorized)
                .ExecuteAsync(http);
        });

    // De dentro, com o biscoito de quem já entrou, ou com a chave no header, como as outras rotas.
    private static bool PodeLer(HttpContext http)
    {
        if (RedeDeDentro.ÉDeDentro(http) || TemCrachá(http))
            return true;

        var enviada = http.Request.Headers[ApiKeyFilter.Header].ToString();

        return enviada.Length > 0
            && http.RequestServices.GetRequiredService<ApiKeyFilter>().Confere(http, enviada);
    }

    private static bool QuerPágina(HttpContext http) =>
        http.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);

    // Se o biscoito está aí, inteiro e dentro da validade.
    private static bool TemCrachá(HttpContext http)
    {
        if (http.Request.Cookies[Biscoito] is not { } guardado)
            return false;

        try
        {
            return long.TryParse(Protetor(http).Unprotect(guardado), out var vence)
                && DateTimeOffset.UtcNow <= DateTimeOffset.FromUnixTimeSeconds(vence);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // biscoito de uma chave antiga (o contêiner subiu de novo) ou mexido na mão
            return false;
        }
    }

    public static void Map(WebApplication app)
    {
        // quem já pode ler não prova de novo: vai direto pro documento
        app.MapGet(Entrar, (HttpContext http) =>
                PodeLer(http) ? Results.Redirect(Documento) : Mostra(errou: false))
            .ExcludeFromDescription();

        app.MapPost(Entrar, async (HttpContext http) =>
            {
                var tentada = http.Request.HasFormContentType
                    ? (await http.Request.ReadFormAsync())[Campo].ToString()
                    : "";

                if (tentada.Length == 0
                    || !http.RequestServices.GetRequiredService<ApiKeyFilter>().Confere(http, tentada))
                {
                    return Mostra(errou: true);
                }

                http.Response.Cookies.Append(Biscoito, Assina(http), new CookieOptions
                {
                    HttpOnly = true,
                    // só volta por HTTPS quando chegou por HTTPS: na tailnet o http:// é o normal
                    Secure = http.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.Add(Validade),
                    Path = CaminhoDoBiscoito,
                });

                return Results.Redirect(Documento);
            })
            .ExcludeFromDescription();

        app.MapGet(Sair, (HttpContext http) =>
            {
                http.Response.Cookies.Delete(Biscoito, new CookieOptions { Path = CaminhoDoBiscoito });
                return Results.Redirect(Entrar);
            })
            .ExcludeFromDescription();
    }

    private static IDataProtector Protetor(HttpContext http) =>
        http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Propósito);

    // O biscoito só guarda até quando vale: existir, assinado por este processo, já diz que entrou.
    private static string Assina(HttpContext http) =>
        Protetor(http).Protect($"{DateTimeOffset.UtcNow.Add(Validade).ToUnixTimeSeconds()}");

    // Chave errada: 401 com a mesma tela, e sem WWW-Authenticate, que abriria a janelinha.
    private static IResult Mostra(bool errou) =>
        Results.Content(
            HtmlDaEntrada(errou),
            Moldura.Tipo,
            statusCode: errou ? StatusCodes.Status401Unauthorized : StatusCodes.Status200OK);

    private static string HtmlDaEntrada(bool errou) => Moldura.De($$"""
          <h1>AlephBot API</h1>
          <p>O documento da API só abre pra quem tem a chave.</p>
          <form method="post" action="{{Entrar}}">
            <label for="{{Campo}}">API_KEY</label>
            <input id="{{Campo}}" name="{{Campo}}" type="password" autocomplete="current-password" autofocus required>
            <button type="submit">Entrar</button>
            {{(errou ? """<p class="erro">Essa chave não abre nada aqui.</p>""" : "")}}
          </form>
        """);
}

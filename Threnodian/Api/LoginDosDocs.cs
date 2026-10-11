using System.Net;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// A tela de entrada dos docs, no lugar da janelinha do navegador. O Basic continua valendo
/// pra quem manda a senha no header (curl, script); quem chega de navegador sem crachá vem
/// parar aqui, e sai com um biscoito assinado dizendo se é dono ou visitante.
///
/// O biscoito é assinado pela proteção de dados do próprio ASP.NET: forjar um exigiria a
/// chave do processo. Ele morre junto com o contêiner — subiu de novo, todo mundo entra de
/// novo, o que pra uma página de documentação é barato.
/// </summary>
internal static class LoginDosDocs
{
    public static readonly string Entrar = $"{ApiDocs.Página}/entrar";
    public static readonly string Sair = $"{ApiDocs.Página}/sair";

    private const string Biscoito = "aleph_docs";
    private const string Propósito = "AlephBot.Docs.v1";
    private const string Campo = "senha";

    private static readonly TimeSpan Validade = TimeSpan.FromDays(30);

    /// <summary>Estas duas ficam fora da senha, senão quem chega pra entrar seria mandado pra cá de novo.</summary>
    public static bool ÉTelaDeEntrada(PathString caminho) =>
        caminho.StartsWithSegments(Entrar) || caminho.StartsWithSegments(Sair);

    /// <summary>O que o biscoito diz, se ele existir, estiver inteiro e não tiver vencido.</summary>
    public static ProteçãoDeFora.Acesso? Crachá(HttpContext http)
    {
        if (http.Request.Cookies[Biscoito] is not { } guardado)
            return null;

        try
        {
            var partes = Protetor(http).Unprotect(guardado).Split('|');

            if (partes.Length != 2 || !long.TryParse(partes[1], out var vence)
                || DateTimeOffset.UtcNow > DateTimeOffset.FromUnixTimeSeconds(vence))
            {
                return null;
            }

            return Enum.TryParse<ProteçãoDeFora.Acesso>(partes[0], out var acesso) ? acesso : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // biscoito de uma chave antiga (o contêiner subiu de novo) ou mexido na mão
            return null;
        }
    }

    public static void Map(WebApplication app, ApiConfig api)
    {
        if (api.DocsPassword is null && api.DocsAdminPassword is null)
            return;

        app.MapGet(Entrar, (HttpContext http) => Mostra(http, errou: false))
            .ExcludeFromDescription();

        app.MapPost(Entrar, async (HttpContext http) =>
        {
            var tentada = http.Request.HasFormContentType
                ? (await http.Request.ReadFormAsync())[Campo].ToString()
                : "";

            if (ProteçãoDeFora.AcessoDaSenha(tentada, api) is not { } acesso)
                return Mostra(http, errou: true);

            http.Response.Cookies.Append(Biscoito, Assina(http, acesso), new CookieOptions
            {
                HttpOnly = true,
                // só volta por HTTPS quando foi por HTTPS que chegou: na tailnet o http:// é o normal
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(Validade),
                Path = ApiDocs.Página,
            });

            return Results.Redirect($"{ApiDocs.Página}/");
        })
        .ExcludeFromDescription();

        app.MapGet(Sair, (HttpContext http) =>
        {
            http.Response.Cookies.Delete(Biscoito, new CookieOptions { Path = ApiDocs.Página });
            return Results.Redirect(Entrar);
        })
        .ExcludeFromDescription();
    }

    private static IDataProtector Protetor(HttpContext http) =>
        http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Propósito);

    private static string Assina(HttpContext http, ProteçãoDeFora.Acesso acesso) =>
        Protetor(http).Protect($"{acesso}|{DateTimeOffset.UtcNow.Add(Validade).ToUnixTimeSeconds()}");

    /// <summary>
    /// Senha errada devolve 401 com a mesma tela: o código conta a verdade pra quem é programa,
    /// e a pessoa vê o aviso. Sem WWW-Authenticate, que é o que faria o navegador abrir a
    /// janelinha de novo por cima da página.
    /// </summary>
    private static IResult Mostra(HttpContext http, bool errou) =>
        Results.Content(
            Html(errou),
            "text/html; charset=utf-8",
            statusCode: errou ? StatusCodes.Status401Unauthorized : StatusCodes.Status200OK);

    /// <summary>
    /// A cara é a mesma linha do Scalar: nada de tema, nada de fonte de fora, e o claro e o
    /// escuro saem do que a pessoa já escolheu no sistema.
    /// </summary>
    private static string Html(bool errou) =>
        $$"""
        <!doctype html>
        <html lang="pt-BR">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex">
        <title>AlephBot API</title>
        <style>
          :root { color-scheme: light dark; --fundo: #fff; --letra: #1a1a1a; --fraco: #6b7280; --borda: #d4d4d8; --campo: #fff; }
          @media (prefers-color-scheme: dark) {
            :root { --fundo: #0f0f11; --letra: #ededef; --fraco: #8b8b94; --borda: #2e2e35; --campo: #17171a; }
          }
          * { box-sizing: border-box; }
          body {
            margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 24px;
            background: var(--fundo); color: var(--letra);
            font: 15px/1.5 ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
          }
          main { width: 100%; max-width: 320px; }
          h1 { margin: 0 0 4px; font-size: 20px; font-weight: 600; letter-spacing: -0.01em; }
          p { margin: 0 0 24px; color: var(--fraco); font-size: 14px; }
          label { display: block; margin-bottom: 6px; font-size: 13px; font-weight: 500; }
          input, button { width: 100%; padding: 9px 12px; border-radius: 6px; font: inherit; }
          input {
            border: 1px solid var(--borda); background: var(--campo); color: var(--letra);
          }
          input:focus { outline: 2px solid var(--letra); outline-offset: -1px; }
          button {
            margin-top: 12px; border: 0; cursor: pointer;
            background: var(--letra); color: var(--fundo); font-weight: 500;
          }
          button:hover { opacity: 0.85; }
          .erro { margin: 12px 0 0; color: #dc2626; font-size: 13px; }
          @media (prefers-color-scheme: dark) { .erro { color: #f87171; } }
        </style>
        </head>
        <body>
        <main>
          <h1>AlephBot API</h1>
          <p>A documentação desta API pede senha.</p>
          <form method="post">
            <label for="{{Campo}}">Senha</label>
            <input id="{{Campo}}" name="{{Campo}}" type="password" autocomplete="current-password" autofocus required>
            <button type="submit">Entrar</button>
            {{(errou ? """<p class="erro">Essa senha não abre nada aqui.</p>""" : "")}}
          </form>
        </main>
        </body>
        </html>
        """;
}

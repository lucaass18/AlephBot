using System.Net;

using AlephBot.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AlephBot.Threnodian.Api;

// A tela de entrada dos docs, no lugar da janelinha. O biscoito morre junto com o contêiner.
internal static class LoginDosDocs
{
    public static readonly string Entrar = $"{ApiDocs.Página}/entrar";
    public static readonly string Sair = $"{ApiDocs.Página}/sair";

    private const string Biscoito = "aleph_docs";
    private const string Propósito = "AlephBot.Docs.v1";
    private const string Campo = "senha";

    private static readonly TimeSpan Validade = TimeSpan.FromDays(30);

    // Estas duas ficam fora da senha, senão quem chega pra entrar seria mandado pra cá de novo.
    public static bool ÉTelaDeEntrada(PathString caminho) =>
        caminho.StartsWithSegments(Entrar) || caminho.StartsWithSegments(Sair);

    // O que o biscoito diz, se ele existir, estiver inteiro e não tiver vencido.
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

        app.MapGet(Entrar, (HttpContext http) =>
                Castigo(http) is { } falta ? DeCastigo(falta) : Mostra(http, errou: false))
            .ExcludeFromDescription();

        app.MapPost(Entrar, async (HttpContext http) =>
        {
            if (Castigo(http) is { } falta)
                return DeCastigo(falta);

            var tentada = http.Request.HasFormContentType
                ? (await http.Request.ReadFormAsync())[Campo].ToString()
                : "";

            if (ProteçãoDeFora.AcessoDaSenha(tentada, api) is not { } acesso)
            {
                Errou(http);
                return Castigo(http) is { } agora ? DeCastigo(agora) : Mostra(http, errou: true);
            }

            Acertou(http);

            http.Response.Cookies.Append(Biscoito, Assina(http, acesso), new CookieOptions
            {
                HttpOnly = true,
                // só volta por HTTPS quando chegou por HTTPS: na tailnet o http:// é o normal
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(Validade),
                Path = ApiDocs.Página,
            });

            return Results.Content(HtmlDoSucesso(acesso), Tipo);
        })
        .ExcludeFromDescription();

        app.MapGet(Sair, (HttpContext http) =>
        {
            http.Response.Cookies.Delete(Biscoito, new CookieOptions { Path = ApiDocs.Página });
            return Results.Redirect(Entrar);
        })
        .ExcludeFromDescription();
    }

    private static (TentativasDeSenha Contador, string Cliente, long Agora) Conta(HttpContext http) => (
        http.RequestServices.GetRequiredService<TentativasDeSenha>(),
        TentativasDeSenha.ClienteDe(http.Connection.RemoteIpAddress),
        Environment.TickCount64);

    // Quanto falta do castigo de quem errou demais, ou null se pode tentar.
    public static TimeSpan? Castigo(HttpContext http)
    {
        var (contador, cliente, agora) = Conta(http);
        return contador.Falta(cliente, agora);
    }

    public static void Errou(HttpContext http)
    {
        var (contador, cliente, agora) = Conta(http);
        contador.Errou(cliente, agora);
    }

    public static void Acertou(HttpContext http)
    {
        var (contador, cliente, _) = Conta(http);
        contador.Acertou(cliente);
    }

    // 429 com Retry-After, como o limite de pedidos: programa lê o header, gente lê a tela.
    private static IResult DeCastigo(TimeSpan falta)
    {
        var minutos = Math.Max(1, (int)Math.Ceiling(falta.TotalMinutes));

        return Results.Content(
            HtmlDoCastigo(minutos),
            Tipo,
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    private static string HtmlDoCastigo(int minutos) => Moldura($$"""
          <h1>Muitas tentativas</h1>
          <p>Foram {{TentativasDeSenha.Limite}} senhas erradas daqui. Tente de novo em
          {{minutos}} {{(minutos == 1 ? "minuto" : "minutos")}}.</p>
        """);

    private static IDataProtector Protetor(HttpContext http) =>
        http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Propósito);

    private static string Assina(HttpContext http, ProteçãoDeFora.Acesso acesso) =>
        Protetor(http).Protect($"{acesso}|{DateTimeOffset.UtcNow.Add(Validade).ToUnixTimeSeconds()}");

    // Senha errada: 401 com a mesma tela, e sem WWW-Authenticate, que reabriria a janelinha.
    private const string Tipo = "text/html; charset=utf-8";

    private static IResult Mostra(HttpContext http, bool errou) =>
        Results.Content(
            HtmlDaEntrada(errou),
            Tipo,
            statusCode: errou ? StatusCodes.Status401Unauthorized : StatusCodes.Status200OK);

    // A tela diz com que crachá entrou, porque é isso que explica o que ela vai achar lá.
    private static string HtmlDoSucesso(ProteçãoDeFora.Acesso acesso) => Moldura($$"""
          <p class="selo">Tudo certo</p>
          <h1>Você entrou{{(acesso == ProteçãoDeFora.Acesso.Dono ? " como dono" : " como visitante")}}</h1>
          <p>{{(acesso == ProteçãoDeFora.Acesso.Dono
              ? "A documentação abre inteira, com a IA do Scalar."
              : "A documentação abre inteira. A IA do Scalar fica de fora, e as rotas que pedem chave seguem pedindo.")}}</p>
          <a class="botão" href="{{ApiDocs.Página}}/">Abrir a documentação</a>
        """);

    // A cara é a mesma linha do Scalar; o claro e o escuro saem do sistema da pessoa.
    private static string HtmlDaEntrada(bool errou) => Moldura($$"""
          <h1>AlephBot API</h1>
          <p>A documentação desta API pede senha.</p>
          <form method="post">
            <label for="{{Campo}}">Senha</label>
            <input id="{{Campo}}" name="{{Campo}}" type="password" autocomplete="current-password" autofocus required>
            <button type="submit">Entrar</button>
            {{(errou ? """<p class="erro">Essa senha não abre nada aqui.</p>""" : "")}}
          </form>
        """);

    private static string Moldura(string miolo) =>
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
          input, button, .botão { width: 100%; padding: 9px 12px; border-radius: 6px; font: inherit; }
          input {
            border: 1px solid var(--borda); background: var(--campo); color: var(--letra);
          }
          input:focus { outline: 2px solid var(--letra); outline-offset: -1px; }
          button, .botão {
            margin-top: 12px; border: 0; cursor: pointer; display: block; text-align: center;
            background: var(--letra); color: var(--fundo); font-weight: 500; text-decoration: none;
          }
          button:hover, .botão:hover { opacity: 0.85; }
          .erro { margin: 12px 0 0; color: #dc2626; font-size: 13px; }
          @media (prefers-color-scheme: dark) { .erro { color: #f87171; } }
          .selo {
            margin: 0 0 8px; font-size: 12px; font-weight: 600; letter-spacing: 0.04em;
            text-transform: uppercase; color: #16a34a;
          }
          @media (prefers-color-scheme: dark) { .selo { color: #4ade80; } }
        </style>
        </head>
        <body>
        <main>
        {{miolo}}
        </main>
        </body>
        </html>
        """;
}

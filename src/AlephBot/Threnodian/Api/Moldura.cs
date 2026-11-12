namespace AlephBot.Threnodian.Api;

// A moldura da tela de entrar: o claro e o escuro saem do sistema de quem abre.
internal static class Moldura
{
    public const string Tipo = "text/html; charset=utf-8";

    public static string De(string miolo) =>
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
          input { border: 1px solid var(--borda); background: var(--campo); color: var(--letra); }
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
        {{miolo}}
        </main>
        </body>
        </html>
        """;
}

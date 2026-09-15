using System.Text;

namespace AlephBot.Core.Commands;

/// <summary>
/// O que todo embed precisa antes de mostrar texto que veio de fora: nome de faixa vem
/// com `[`, `]` e `*` o tempo todo, e um colchete solto quebra a sintaxe do markdown do
/// Discord. O título de anime também — "[Oshi no Ko]" existe.
/// </summary>
internal static class Markdown
{
    internal static string Escapa(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return "";

        var saída = new StringBuilder(texto.Length + 8);

        foreach (var c in texto)
        {
            if (c is '[' or ']' or '(' or ')' or '*' or '_' or '~' or '`' or '\\' or '|' or '>')
                saída.Append('\\');

            saída.Append(c);
        }

        return saída.ToString();
    }
}

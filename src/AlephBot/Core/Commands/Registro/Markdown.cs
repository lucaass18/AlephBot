using System.Text;

namespace AlephBot.Core.Commands.Registro;

// Texto de fora vem com `[`, `]` e `*`, e um colchete solto quebra o markdown do Discord.
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

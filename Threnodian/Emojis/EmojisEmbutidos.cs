using System.Reflection;

namespace AlephBot.Threnodian.Emojis;

// Os PNGs da ficha, que vão dentro do dll. O bot sobe os que faltam no app, pra ninguém ter
// que arrastar arquivo no Developer Portal — nem agora, nem num app novo amanhã.
internal static class EmojisEmbutidos
{
    private const string Sufixo = ".png";

    // Só os da ficha: o prefixo evita subir qualquer png que entre no assembly por outro motivo.
    private const string Prefixo = "ma_";

    private static readonly Assembly Assembly = typeof(EmojisEmbutidos).GetTypeInfo().Assembly;

    // Os nomes de emoji que o assembly carrega, sem o caminho e sem a extensão.
    internal static IReadOnlyList<string> Nomes() =>
        [.. Assembly.GetManifestResourceNames()
            .Select(Nome)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.Ordinal)];

    // Os bytes de um emoji embutido, ou null quando ele não está no assembly.
    internal static byte[]? Bytes(string nome)
    {
        var recurso = Array.Find(
            Assembly.GetManifestResourceNames(),
            r => string.Equals(Nome(r), nome, StringComparison.Ordinal));

        if (recurso is null)
            return null;

        using var fluxo = Assembly.GetManifestResourceStream(recurso);

        if (fluxo is null)
            return null;

        using var memória = new MemoryStream();
        fluxo.CopyTo(memória);

        return memória.ToArray();
    }

    // O que o Discord aceita no campo image: o png em data URI.
    internal static string ParaDataUri(byte[] png) =>
        "data:image/png;base64," + Convert.ToBase64String(png);

    // Quem ainda não está no app. É o que separa o primeiro boot dos outros todos.
    internal static IReadOnlyList<string> QuaisFaltam(IEnumerable<string> noApp) =>
        [.. Nomes().Except(noApp, StringComparer.Ordinal)];

    private static string? Nome(string recurso)
    {
        if (!recurso.EndsWith(Sufixo, StringComparison.OrdinalIgnoreCase))
            return null;

        var semExtensão = recurso[..^Sufixo.Length];
        var ponto = semExtensão.LastIndexOf('.');
        var nome = ponto < 0 ? semExtensão : semExtensão[(ponto + 1)..];

        return nome.StartsWith(Prefixo, StringComparison.Ordinal) ? nome : null;
    }
}

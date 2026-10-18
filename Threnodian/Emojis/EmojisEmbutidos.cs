using System.Reflection;

namespace AlephBot.Threnodian.Emojis;

// Os PNGs da ficha, que vão dentro do dll. O bot sobe os que faltam no app, pra ninguém ter
// que arrastar arquivo no Developer Portal — nem agora, nem num app novo amanhã.
internal static class EmojisEmbutidos
{
    // GIF é emoji animado; o Discord aceita os dois.
    private static readonly string[] Sufixos = [".png", ".gif"];

    // Vale o que estiver numa pasta Emojis: a da ficha e a da Denia. O recorte é a pasta, e não
    // um prefixo de nome, pra png que entre no assembly por outro motivo não virar emoji.
    private const string Pasta = ".Emojis.";

    // O que o Discord aceita como nome: letra, número e sublinhado, de 2 a 32.
    private static bool NomeVálido(string nome) =>
        nome.Length is >= 2 and <= 32 && nome.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

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

    // O que o Discord aceita no campo image. O tipo sai dos primeiros bytes e não da extensão:
    // GIF entregue como image/png sobe parado, e o erro só apareceria olhando o emoji.
    internal static string ParaDataUri(byte[] imagem) =>
        $"data:{Tipo(imagem)};base64,{Convert.ToBase64String(imagem)}";

    internal static string Tipo(byte[] imagem) =>
        imagem.Length >= 3 && imagem[0] == (byte)'G' && imagem[1] == (byte)'I' && imagem[2] == (byte)'F'
            ? "image/gif"
            : "image/png";

    // Quem ainda não está no app. É o que separa o primeiro boot dos outros todos.
    internal static IReadOnlyList<string> QuaisFaltam(IEnumerable<string> noApp) =>
        [.. Nomes().Except(noApp, StringComparer.Ordinal)];

    private static string? Nome(string recurso)
    {
        var sufixo = Array.Find(Sufixos, s => recurso.EndsWith(s, StringComparison.OrdinalIgnoreCase));

        if (sufixo is null || !recurso.Contains(Pasta, StringComparison.Ordinal))
            return null;

        var semExtensão = recurso[..^sufixo.Length];
        var ponto = semExtensão.LastIndexOf('.');
        var nome = ponto < 0 ? semExtensão : semExtensão[(ponto + 1)..];

        return NomeVálido(nome) ? nome : null;
    }
}

using System.Collections.Concurrent;

namespace AlephBot.Threnodian.Emojis;

// Os emoji do próprio app, resolvidos pelo nome. Emoji customizado em embed só existe como
// <:nome:id>, e o id é do Discord — por isso o bot pergunta a ele no boot em vez de guardar.
// Mapa vazio é o normal: sem os emoji subidos, os rótulos saem só com a palavra.
public static class EmojisDaFicha
{
    private static readonly ConcurrentDictionary<string, string> Marcações = new(StringComparer.Ordinal);

    // Quantos o app tem agora. Serve pro log do boot dizer se achou alguma coisa.
    public static int Quantos => Marcações.Count;

    // A marcação pronta pro Discord, ou null quando o emoji não foi subido.
    public static string? Marcação(string nome) =>
        Marcações.TryGetValue(nome, out var marcação) ? marcação : null;

    // O rótulo do campo: com o emoji na frente quando ele existe, só a palavra quando não.
    public static string Rótulo(string nome, string palavra) =>
        Marcação(nome) is { } marcação ? $"{marcação} {palavra}" : palavra;

    internal static void Guarda(string nome, string id, bool animado) =>
        Marcações[nome] = animado ? $"<a:{nome}:{id}>" : $"<:{nome}:{id}>";

    // Só os testes esvaziam: no bot isto é preenchido uma vez, no boot.
    internal static void Esquece() => Marcações.Clear();
}

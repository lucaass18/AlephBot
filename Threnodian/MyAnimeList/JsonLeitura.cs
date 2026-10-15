using System.Text.Json;

namespace AlephBot.Threnodian.MyAnimeList;

// Leitura tolerante: campo ausente, null ou do tipo errado vira null, em vez de exceção.
internal static class JsonLeitura
{
    public static JsonElement? Campo(this JsonElement elemento, string nome) =>
        elemento.ValueKind == JsonValueKind.Object
        && elemento.TryGetProperty(nome, out var valor)
        && valor.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? valor
            : null;

    public static string? Texto(this JsonElement elemento, string nome) =>
        elemento.Campo(nome) is { ValueKind: JsonValueKind.String } valor
            ? valor.GetString()
            : null;

    // Só o que é positivo: o MAL escreve 0 onde ainda não sabe quantos episódios tem.
    public static int? Contagem(this JsonElement elemento, string nome) =>
        elemento.Campo(nome) is { ValueKind: JsonValueKind.Number } valor
        && valor.TryGetInt32(out var número)
        && número > 0
            ? número
            : null;

    public static double? Decimal(this JsonElement elemento, string nome) =>
        elemento.Campo(nome) is { ValueKind: JsonValueKind.Number } valor
            ? valor.GetDouble()
            : null;

    public static IEnumerable<JsonElement> Lista(this JsonElement elemento, string nome) =>
        elemento.Campo(nome) is { ValueKind: JsonValueKind.Array } valor
            ? valor.EnumerateArray()
            : [];

    // Os textos de uma lista de strings, sem os vazios.
    public static List<string> Textos(this JsonElement elemento, string nome) =>
        elemento.Lista(nome)
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(texto => !string.IsNullOrWhiteSpace(texto))
            .ToList();

    // Um campo de texto de cada objeto da lista: o "name" de cada gênero, por exemplo.
    public static List<string> Nomes(this JsonElement elemento, string lista, string campo = "name") =>
        elemento.Lista(lista)
            .Select(item => item.Texto(campo))
            .Where(nome => !string.IsNullOrWhiteSpace(nome))
            .Select(nome => nome!)
            .ToList();
}

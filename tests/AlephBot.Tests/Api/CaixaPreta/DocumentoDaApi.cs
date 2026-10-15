using System.Text.Json;
using System.Text.Json.Nodes;

using Json.Schema;

namespace AlephBot.Tests.Api.CaixaPreta;

// O documento OpenAPI lido de fora: é a única fonte das asserções da caixa preta.
public sealed class DocumentoDaApi
{
    public const string Caminho = "/api/v1/openapi.json";

    // O dialeto do OpenAPI 3.1: os schemas dele são JSON Schema 2020-12.
    private const string Dialeto = "https://json-schema.org/draft/2020-12/schema";

    private const string Problema = "application/problem+json";

    private readonly JsonObject _definições;

    private DocumentoDaApi(JsonObject raiz)
    {
        Raiz = raiz;

        // os componentes viram $defs, pra cada schema virar um documento 2020-12 que se basta
        _definições = (JsonObject)ParaDefs(raiz["components"]?["schemas"]?.DeepClone() ?? new JsonObject())!;

        HeaderDaChave = LêOHeaderDaChave(raiz);
        Operações = [.. LêOperações(raiz)];
    }

    public JsonObject Raiz { get; }

    public IReadOnlyList<OperaçãoPrometida> Operações { get; }

    // Em que header vai a chave, segundo o próprio documento.
    public string HeaderDaChave { get; }

    // O formato de erro sai das próprias respostas problem+json, não de um nome escrito aqui.
    public RespostaPrometida FormatoDeErro => Operações
        .SelectMany(operação => operação.Respostas)
        .First(resposta => resposta.TipoDeConteúdo == Problema);

    // Os nomes que o documento declara em components/schemas.
    public IReadOnlyCollection<string> SchemasDeclarados =>
        [.. Componentes?.Select(schema => schema.Key) ?? []];

    // Os schemas alcançáveis pelos $ref das respostas; o que sobra é peso morto no documento.
    public IReadOnlyCollection<string> SchemasAlcançáveis
    {
        get
        {
            HashSet<string> achados = [];

            Queue<JsonNode> fila = new(Operações
                .SelectMany(operação => operação.Respostas)
                .Where(resposta => resposta.Schema is not null)
                .Select(resposta => resposta.Schema!));

            while (fila.TryDequeue(out var nó))
            {
                foreach (var nome in Referências(nó))
                {
                    if (achados.Add(nome) && Componentes?[nome] is { } schema)
                        fila.Enqueue(schema);
                }
            }

            return achados;
        }
    }

    private JsonObject? Componentes => Raiz["components"]?["schemas"]?.AsObject();

    public static async Task<DocumentoDaApi> LerAsync(ApiDeTeste api)
    {
        var (status, corpo) = await api.GetAsync(Caminho);

        Assert.Equal(200, status);
        return new DocumentoDaApi((JsonObject)JsonNode.Parse(corpo)!);
    }

    // O corpo bate com o schema? O format conta: data torta quebra cliente gerado do documento.
    public EvaluationResults Confere(JsonNode schema, string corpo)
    {
        var completo = (JsonObject)ParaDefs(schema.DeepClone())!;
        completo["$schema"] = Dialeto;
        completo["$defs"] = _definições.DeepClone();

        using var instância = JsonDocument.Parse(corpo);

        return JsonSchema.FromText(completo.ToJsonString()).Evaluate(
            instância.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
    }

    // O que o validador achou de errado, pra mensagem de falha dizer o que quebrou.
    public static string Explica(EvaluationResults conferência) =>
        JsonSerializer.Serialize(conferência, new JsonSerializerOptions { WriteIndented = true });

    // Em que header a chave vai, lido do documento — é o que faz a caixa preta ser caixa preta.
    private static string LêOHeaderDaChave(JsonObject raiz)
    {
        var esquema = raiz["security"]?.AsArray()
            .SelectMany(exigência => exigência!.AsObject().Select(par => par.Key))
            .FirstOrDefault();

        return (string?)raiz["components"]?["securitySchemes"]?[esquema ?? ""]?["name"]
            ?? throw new InvalidOperationException("o documento não diz em que header vai a chave");
    }

    private static IEnumerable<OperaçãoPrometida> LêOperações(JsonObject raiz)
    {
        // o que o documento exige de quem chama quando a operação não diz nada por si
        var padrão = raiz["security"];

        foreach (var (caminho, operações) in raiz["paths"]!.AsObject())
        {
            foreach (var (método, operação) in operações!.AsObject())
            {
                yield return new OperaçãoPrometida(
                    Id: (string?)operação!["operationId"] ?? $"{método} {caminho}",
                    Método: método.ToUpperInvariant(),
                    Caminho: caminho,
                    PedeChave: Exige(operação["security"] ?? padrão),
                    Respostas: [.. LêRespostas(operação["responses"]!.AsObject())]);
            }
        }
    }

    // "security": [{}] é a rota dizendo que não pede nada; herdar o do documento é pedir.
    private static bool Exige(JsonNode? security) =>
        security is JsonArray exigências
        && exigências.Any(exigência => exigência is JsonObject esquemas && esquemas.Count > 0);

    private static IEnumerable<RespostaPrometida> LêRespostas(JsonObject respostas)
    {
        foreach (var (status, resposta) in respostas)
        {
            // uma resposta da API sai num tipo só; o documento também declara um só por resposta
            var conteúdo = resposta!["content"]?.AsObject().FirstOrDefault();

            yield return new RespostaPrometida(
                Status: status,
                TipoDeConteúdo: conteúdo?.Key,
                Schema: conteúdo?.Value?["schema"],
                Headers: [.. resposta["headers"]?.AsObject().Select(header => header.Key) ?? []]);
        }
    }

    // Os schemas que este pedaço do documento referencia, pelo nome.
    private static IEnumerable<string> Referências(JsonNode? nó)
    {
        const string prefixo = "#/components/schemas/";

        switch (nó)
        {
            case JsonObject objeto:
                foreach (var (chave, valor) in objeto)
                {
                    if (chave == "$ref" && (string?)valor is { } alvo && alvo.StartsWith(prefixo))
                    {
                        yield return alvo[prefixo.Length..];
                    }
                    else
                    {
                        foreach (var achado in Referências(valor))
                            yield return achado;
                    }
                }

                break;

            case JsonArray lista:
                foreach (var item in lista)
                {
                    foreach (var achado in Referências(item))
                        yield return achado;
                }

                break;
        }
    }

    // "#/components/schemas/X" vira "#/$defs/X", que é onde o JSON Schema procura.
    private static JsonNode? ParaDefs(JsonNode? nó)
    {
        switch (nó)
        {
            case JsonObject objeto:
                foreach (var (chave, valor) in objeto.ToList())
                {
                    if (chave == "$ref" && (string?)valor is { } alvo)
                        objeto[chave] = alvo.Replace("#/components/schemas/", "#/$defs/");
                    else
                        ParaDefs(valor);
                }

                return objeto;

            case JsonArray lista:
                foreach (var item in lista)
                    ParaDefs(item);

                return lista;

            default:
                return nó;
        }
    }
}

// Uma resposta que o documento promete: o status, o tipo do corpo, o schema e os headers.
public sealed record RespostaPrometida(
    string Status,
    string? TipoDeConteúdo,
    JsonNode? Schema,
    IReadOnlyList<string> Headers)
{
    // O HEAD do health é o caso de resposta sem corpo nenhum.
    public bool TemCorpo => Schema is not null;
}

// Uma operação do documento: o que chamar, se ela pede chave e o que promete devolver.
public sealed record OperaçãoPrometida(
    string Id,
    string Método,
    string Caminho,
    bool PedeChave,
    IReadOnlyList<RespostaPrometida> Respostas)
{
    public RespostaPrometida? Resposta(int status) =>
        Respostas.FirstOrDefault(resposta => resposta.Status == status.ToString());

    public override string ToString() => $"{Método} {Caminho}";
}

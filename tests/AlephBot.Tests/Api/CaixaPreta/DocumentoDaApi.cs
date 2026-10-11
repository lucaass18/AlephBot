using System.Text.Json;
using System.Text.Json.Nodes;

using Json.Schema;

namespace AlephBot.Tests.Api.CaixaPreta;

/// <summary>
/// O documento OpenAPI lido como quem está de fora lê: sem abrir o Contracts.cs, sem saber o
/// nome de um record sequer. Daqui saem as operações que a API anuncia, o schema de cada
/// resposta e até o nome do header da chave — é esta a única fonte das asserções da caixa
/// preta. Rota nova no documento entra nos testes sozinha; promessa que o documento faz e a
/// API não cumpre quebra teste.
/// </summary>
public sealed class DocumentoDaApi
{
    public const string Caminho = "/api/v1/openapi.json";

    /// <summary>O dialeto do OpenAPI 3.1: os schemas dele são JSON Schema 2020-12.</summary>
    private const string Dialeto = "https://json-schema.org/draft/2020-12/schema";

    private const string Problema = "application/problem+json";

    private readonly JsonObject _definições;

    private DocumentoDaApi(JsonObject raiz)
    {
        Raiz = raiz;

        // os componentes viram $defs: assim o schema de cada resposta vira um documento 2020-12
        // que se basta, sem depender de registry global nem de URI de fora
        _definições = (JsonObject)ParaDefs(raiz["components"]?["schemas"]?.DeepClone() ?? new JsonObject())!;

        HeaderDaChave = LêOHeaderDaChave(raiz);
        Operações = [.. LêOperações(raiz)];
    }

    public JsonObject Raiz { get; }

    public IReadOnlyList<OperaçãoPrometida> Operações { get; }

    /// <summary>Em que header vai a chave, segundo o próprio documento.</summary>
    public string HeaderDaChave { get; }

    /// <summary>
    /// O formato com que a API conta um erro. Sai das respostas de erro das próprias operações
    /// (as de <c>application/problem+json</c>), e não de um nome escrito aqui: se o documento
    /// trocar de formato de erro, o teste acompanha.
    /// </summary>
    public RespostaPrometida FormatoDeErro => Operações
        .SelectMany(operação => operação.Respostas)
        .First(resposta => resposta.TipoDeConteúdo == Problema);

    /// <summary>Os nomes que o documento declara em <c>components/schemas</c>.</summary>
    public IReadOnlyCollection<string> SchemasDeclarados =>
        [.. Componentes?.Select(schema => schema.Key) ?? []];

    /// <summary>
    /// Os schemas que alguém alcança partindo das respostas das operações e seguindo os
    /// <c>$ref</c>. O que sobra é peso morto: documento anunciando formato que não sai por
    /// rota nenhuma.
    /// </summary>
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

    /// <summary>
    /// O corpo bate com o schema prometido? O <c>format</c> conta: data fora do formato é
    /// quebra de contrato pra quem gerou um cliente a partir deste documento.
    /// </summary>
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

    /// <summary>O que o validador achou de errado, pra mensagem de falha dizer o que quebrou.</summary>
    public static string Explica(EvaluationResults conferência) =>
        JsonSerializer.Serialize(conferência, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// O esquema que o documento exige de quem chama diz em que header a chave vai. Ler daqui,
    /// e não escrever "X-Api-Key" no teste, é o que faz a caixa preta ser caixa preta.
    /// </summary>
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

    /// <summary>
    /// "security": [{}] é a rota dizendo que não pede nada. Herdar o do documento, que lista a
    /// chave, é pedir.
    /// </summary>
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

    /// <summary>Os schemas que este pedaço do documento referencia, pelo nome.</summary>
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

    /// <summary>
    /// "#/components/schemas/X" vira "#/$defs/X". O OpenAPI guarda os schemas onde o JSON
    /// Schema não procura; movidos pra $defs, o schema de uma resposta se resolve sozinho.
    /// </summary>
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

/// <summary>Uma resposta que o documento promete: o status, o tipo do corpo, o schema e os headers.</summary>
public sealed record RespostaPrometida(
    string Status,
    string? TipoDeConteúdo,
    JsonNode? Schema,
    IReadOnlyList<string> Headers)
{
    /// <summary>O HEAD do health é o caso de resposta sem corpo nenhum.</summary>
    public bool TemCorpo => Schema is not null;
}

/// <summary>Uma operação do documento: o que chamar, se ela pede chave e o que promete devolver.</summary>
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

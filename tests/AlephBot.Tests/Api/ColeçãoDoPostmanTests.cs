using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using AlephBot.Threnodian.Api;

namespace AlephBot.Tests.Api;

// A coleção do Postman conferida contra o documento: rota que nascer na API nasce nela também.
public class ColeçãoDoPostmanTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;

    // a pasta Postman da raiz, a partir deste arquivo: tests/AlephBot.Tests/Api
    private static string Pasta([CallerFilePath] string arquivo = "") =>
        Path.Combine(Path.GetDirectoryName(arquivo)!, "..", "..", "..", "Postman");

    private static JsonNode Arquivo(string nome) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta(), nome)))!;

    private static JsonNode Coleção() => Arquivo("AlephBot.postman_collection.json");

    private static JsonNode Ambiente() => Arquivo("AlephBot.postman_environment.json");

    // as pastas com o que o documento não descreve: ele mesmo, a tela de entrar e os endereços de antes
    private static readonly string[] ForaDoDocumento = ["Documento", "Endereços de antes"];

    // "GET /api/v1/health" por pedido, no mesmo formato em que o documento nomeia as rotas
    private static List<string> Pedidos(bool foraDoDocumento = false) =>
    [
        .. Coleção()["item"]!.AsArray()
            .Where(pasta => ForaDoDocumento.Contains((string?)pasta!["name"]) == foraDoDocumento)
            .SelectMany(pasta => pasta!["item"]!.AsArray())
            .Select(pedido => pedido!["request"]!)
            .Select(pedido => $"{(string?)pedido["method"]} "
                + ((string?)pedido["url"]!["raw"])!.Replace("{{baseUrl}}", "")),
    ];

    private async Task<List<string>> DoDocumentoAsync()
    {
        var (_, corpo) = await _api.GetAsync("/api/v1/openapi.json");

        return
        [
            .. JsonNode.Parse(corpo)!["paths"]!.AsObject()
                .SelectMany(rota => rota.Value!.AsObject()
                    .Select(operação => $"{operação.Key.ToUpperInvariant()} {rota.Key}")),
        ];
    }

    [Fact]
    public async Task Toda_rota_do_documento_tem_pedido_na_coleção()
    {
        var pedidos = Pedidos();

        foreach (var rota in await DoDocumentoAsync())
            Assert.Contains(rota, pedidos);
    }

    [Fact]
    public async Task A_coleção_não_inventa_rota_que_a_API_não_tem()
    {
        var documento = await DoDocumentoAsync();

        foreach (var pedido in Pedidos())
            Assert.Contains(pedido, documento);
    }

    [Fact]
    public async Task O_que_fica_fora_do_documento_existe_na_API()
    {
        var pedidos = Pedidos(foraDoDocumento: true);
        Assert.NotEmpty(pedidos);

        foreach (var pedido in pedidos)
        {
            var (método, caminho) = (pedido.Split(' ')[0], pedido.Split(' ')[1]);

            var resposta = (await _api.Servidor.SendAsync(http =>
            {
                http.Request.Method = método;
                http.Request.Path = caminho;
                http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            })).Response;

            Assert.True(
                resposta.StatusCode is not (404 or 405),
                $"{pedido} respondeu {resposta.StatusCode}");
        }
    }

    [Fact]
    public void A_chave_vai_no_header_que_a_API_espera()
    {
        var auth = Coleção()["auth"]!["apikey"]!.AsArray();

        string? Valor(string nome) => (string?)auth.First(campo => (string?)campo!["key"] == nome)!["value"];

        Assert.Equal(ApiKeyFilter.Header, Valor("key"));
        Assert.Equal("header", Valor("in"));
        Assert.Equal("{{apiKey}}", Valor("value"));
    }

    [Fact]
    public void O_environment_não_leva_chave_nenhuma_pro_repositório()
    {
        // a chave entra na máquina de quem importa, ou no --env-var do Newman; aqui, nunca
        var chave = Ambiente()["values"]!.AsArray()
            .First(valor => (string?)valor!["key"] == "apiKey")!;

        Assert.Equal("", (string?)chave["value"]);
        Assert.Equal("secret", (string?)chave["type"]);
    }
}

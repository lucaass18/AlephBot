using System.Text.Json.Nodes;

namespace AlephBot.Tests.Api;

/// <summary>
/// A versão mora no caminho (/api/v1/...). Quem usava os endereços de antes não quebra: o
/// health continua respondendo onde estava, e o resto manda pra versão atual.
/// </summary>
public class VersionamentoTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;

    [Fact]
    public async Task Respostas_da_v1_contam_as_versões_que_existem()
    {
        var (status, _, headers) = await _api.GetComHeadersAsync("/api/v1/status");

        Assert.Equal(200, status);
        Assert.Contains("1", headers["api-supported-versions"].ToString());
    }

    [Fact]
    public async Task Health_de_antes_continua_respondendo_pros_monitores()
    {
        var (status, corpo) = await _api.GetAsync("/api/health");

        Assert.Equal(503, status);
        Assert.Equal("down", (string?)JsonNode.Parse(corpo)!["status"]);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("stats")]
    [InlineData("commands")]
    public async Task Endereço_de_antes_manda_pra_v1(string rota)
    {
        var (status, _, headers) = await _api.GetComHeadersAsync($"/api/{rota}");

        Assert.Equal(308, status);
        Assert.Equal($"/api/v1/{rota}", headers.Location.ToString());
    }

    [Fact]
    public async Task Documento_de_antes_manda_pro_da_v1()
    {
        var (status, _, headers) = await _api.GetComHeadersAsync("/api/openapi.json");

        Assert.Equal(301, status);
        Assert.Equal("/api/openapi/v1.json", headers.Location.ToString());
    }

    [Fact]
    public async Task Versão_que_não_existe_é_endereço_que_não_existe()
    {
        // com a versão no caminho, /api/v2/status é só um endereço que não existe
        var (status, _) = await _api.GetAsync("/api/v2/status");

        Assert.Equal(404, status);
    }

    [Fact]
    public async Task O_documento_da_v1_só_tem_caminho_versionado()
    {
        var (_, corpo) = await _api.GetAsync("/api/openapi/v1.json");
        var documento = JsonNode.Parse(corpo)!;

        Assert.Equal("1", (string?)documento["info"]!["version"]);
        Assert.All(documento["paths"]!.AsObject(), rota => Assert.StartsWith("/api/v1/", rota.Key));
    }

    [Fact]
    public async Task A_página_mostra_o_documento_da_v1()
    {
        var (_, html) = await _api.GetAsync("/api/docs/");

        Assert.Contains("api/openapi/v1.json", html);
        Assert.Contains("AlephBot API v1", html);
    }
}

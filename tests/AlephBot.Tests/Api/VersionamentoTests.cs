using System.Text.Json.Nodes;

namespace AlephBot.Tests.Api;

// A versão mora no caminho. O health segue onde estava, e o resto manda pra versão atual.
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
        Assert.Equal("/api/v1/openapi.json", headers.Location.ToString());
    }

    [Fact]
    public async Task A_raiz_leva_pra_tela_de_entrar()
    {
        var (status, _, headers) = await _api.GetComHeadersAsync("/");

        Assert.Equal(302, status);
        Assert.Equal("/api/docs", headers.Location.ToString());
    }

    [Fact]
    public async Task De_dentro_a_tela_de_entrar_passa_direto_pro_documento_da_versão_atual()
    {
        // 302 porque, quando nascer a v2, é pra ela que a entrada vai levar
        var (status, _, headers) = await _api.GetComHeadersAsync("/api/docs");

        Assert.Equal(302, status);
        Assert.Equal("/api/v1/openapi.json", headers.Location.ToString());
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
        var (_, corpo) = await _api.GetAsync("/api/v1/openapi.json");
        var documento = JsonNode.Parse(corpo)!;

        Assert.Equal("1", (string?)documento["info"]!["version"]);
        Assert.All(documento["paths"]!.AsObject(), rota => Assert.StartsWith("/api/v1/", rota.Key));
    }

    [Fact]
    public async Task Documento_de_versão_que_não_existe_dá_404()
    {
        var (status, _) = await _api.GetAsync("/api/v2/openapi.json");

        Assert.Equal(404, status);
    }
}

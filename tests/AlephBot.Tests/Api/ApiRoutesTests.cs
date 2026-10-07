using System.Text.Json.Nodes;

using AlephBot.Core.Personality;
using AlephBot.Threnodian.Api;

namespace AlephBot.Tests.Api;

/// <summary>Quem passa sem chave, quem não passa, e o que cada rota responde com o bot ainda desconectado.</summary>
public class ApiRoutesTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;

    private static (string, string) Chave(string valor) => (ApiKeyFilter.Header, valor);

    [Fact]
    public async Task Health_não_pede_chave_e_dá_503_sem_Discord()
    {
        var (status, corpo) = await _api.GetAsync("/api/health");
        var json = JsonNode.Parse(corpo)!;

        Assert.Equal(503, status);
        Assert.Equal("down", (string?)json["status"]);
        Assert.Equal("disconnected", (string?)json["discord"]);
        Assert.Equal("connecting", (string?)json["lavalink"]);
    }

    [Fact]
    public async Task Status_não_pede_chave()
    {
        var (status, corpo) = await _api.GetAsync("/api/status");
        var json = JsonNode.Parse(corpo)!;

        Assert.Equal(200, status);
        Assert.Equal("1.0.0", (string?)json["version"]);
        Assert.Null(json["bot"]);
        Assert.Equal("disconnected", (string?)json["discord"]!["status"]);
        Assert.Null(json["discord"]!["latencyMs"]);
        Assert.Equal("connecting", (string?)json["lavalink"]!["status"]);
    }

    [Theory]
    [InlineData("/api/stats")]
    [InlineData("/api/commands")]
    public async Task Sem_chave_leva_401(string rota)
    {
        var (status, corpo) = await _api.GetAsync(rota);

        Assert.Equal(401, status);
        Assert.Equal(Denia.ApiSemChave(ApiKeyFilter.Header), (string?)JsonNode.Parse(corpo)!["detail"]);
    }

    [Theory]
    [InlineData("/api/stats")]
    [InlineData("/api/commands")]
    public async Task Chave_errada_leva_401(string rota)
    {
        var (status, corpo) = await _api.GetAsync(rota, headers: Chave("chave-errada-1234567890"));

        Assert.Equal(401, status);
        Assert.Equal(Denia.ApiChaveErrada(), (string?)JsonNode.Parse(corpo)!["detail"]);
    }

    [Fact]
    public async Task Com_a_chave_certa_os_comandos_saem()
    {
        var (status, corpo) = await _api.GetAsync("/api/commands", headers: Chave(ApiDeTeste.ChaveDaApi));
        var comandos = JsonNode.Parse(corpo)!.AsArray();

        Assert.Equal(200, status);
        Assert.Contains(comandos, c => (string?)c!["name"] == "ping" && (string?)c["slash"] == "/ping");
    }

    [Fact]
    public async Task Rota_que_não_existe_dá_404()
    {
        var (status, _) = await _api.GetAsync("/api/nada");

        Assert.Equal(404, status);
    }
}

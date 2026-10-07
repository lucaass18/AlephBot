using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AlephBot.Tests.Api;

/// <summary>
/// O /api/docs e o que ele carrega. A regra que importa: de fora (pelo Funnel ou com IP
/// público) ninguém leva chave nenhuma — nem a da API, nem a da IA do Scalar.
/// </summary>
public partial class ApiDocsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;

    private static readonly (string, string) PeloFunnel = ("Tailscale-Funnel-Request", "?1");

    private async Task<JsonNode> DocumentoAsync()
    {
        var (_, corpo) = await _api.GetAsync("/api/openapi/v1.json");
        return JsonNode.Parse(corpo)!;
    }

    // ---- documento -----------------------------------------------------------

    [Fact]
    public async Task Cada_operação_tem_nome()
    {
        var operações = (await DocumentoAsync())["paths"]!.AsObject()
            .SelectMany(rota => rota.Value!.AsObject().Select(op => (string?)op.Value!["operationId"]))
            .ToList();

        Assert.Equal(new[] { "getHealth", "headHealth", "getStatus", "getStats", "getCommands" }, operações);
    }

    [Fact]
    public async Task A_chave_vale_pro_documento_menos_no_health_e_no_status()
    {
        var documento = await DocumentoAsync();
        var rotas = documento["paths"]!;

        Assert.Equal("""[{"ApiKey":[]}]""", documento["security"]!.ToJsonString());
        Assert.Equal("[{}]", rotas["/api/v1/health"]!["get"]!["security"]!.ToJsonString());
        Assert.Equal("[{}]", rotas["/api/v1/status"]!["get"]!["security"]!.ToJsonString());
        Assert.Null(rotas["/api/v1/stats"]!["get"]!["security"]);
        Assert.Null(rotas["/api/v1/commands"]!["get"]!["security"]);
    }

    [Fact]
    public async Task Do_bot_só_sai_o_nome()
    {
        // o ID não sai em rota nenhuma, nem o avatar, cujo link carrega o ID dentro
        var bot = (await DocumentoAsync())["components"]!["schemas"]!["BotIdentity"]!["properties"]!.AsObject();

        Assert.Equal(["username"], bot.Select(p => p.Key));
    }

    [Fact]
    public async Task O_esquema_da_chave_é_o_header_X_Api_Key()
    {
        var esquema = (await DocumentoAsync())["components"]!["securitySchemes"]!["ApiKey"]!;

        Assert.Equal("apiKey", (string?)esquema["type"]);
        Assert.Equal("header", (string?)esquema["in"]);
        Assert.Equal("X-Api-Key", (string?)esquema["name"]);
    }

    [Fact]
    public async Task Pelo_Funnel_o_documento_anuncia_https()
    {
        // o Tailscale termina o HTTPS e repassa em HTTP, contando em X-Forwarded-Proto
        var (_, corpo) = await _api.GetAsync(
            "/api/openapi/v1.json",
            host: "aleph.tailc38add.ts.net",
            headers: [PeloFunnel, ("X-Forwarded-Proto", "https"), ("X-Forwarded-For", "203.0.113.9")]);

        Assert.Equal("https://aleph.tailc38add.ts.net", (string?)JsonNode.Parse(corpo)!["servers"]![0]!["url"]);
    }

    // ---- página --------------------------------------------------------------

    [GeneratedRegex("""\{"authentication".*\}(?=,\s*\n)""")]
    private static partial Regex ConfigDaPágina();

    private async Task<string> PáginaAsync(params (string, string)[] headers)
    {
        var (status, html) = await _api.GetAsync("/api/docs/", headers: headers);

        Assert.Equal(200, status);
        return html;
    }

    [Fact]
    public async Task Na_rede_de_casa_a_página_tem_a_IA_e_a_chave_automática()
    {
        var html = await PáginaAsync();
        var config = ConfigDaPágina().Match(html).Value;

        Assert.Contains(ApiDeTeste.ChaveDoAgent, config);
        Assert.Contains("/api/docs/aleph.js", html);
        Assert.Contains("\"persistAuth\":true", config);
    }

    [Fact]
    public async Task Pelo_Funnel_a_página_tem_a_IA_mas_não_a_chave_da_API()
    {
        var html = await PáginaAsync(PeloFunnel);
        var config = ConfigDaPágina().Match(html).Value;

        // a IA vem (a chave dela é do navegador, por natureza); a da API, não
        Assert.Contains(ApiDeTeste.ChaveDoAgent, config);
        Assert.DoesNotContain(ApiDeTeste.ChaveDaApi, html);
        Assert.DoesNotContain("aleph.js", html);
        Assert.Contains("\"persistAuth\":false", config);
    }

    [Fact]
    public async Task IP_público_direto_também_é_tratado_como_de_fora()
    {
        var (_, html) = await _api.GetAsync("/api/docs/", IPAddress.Parse("203.0.113.9"));

        Assert.DoesNotContain("aleph.js", html);
        Assert.Contains("\"persistAuth\":false", ConfigDaPágina().Match(html).Value);
    }

    [Fact]
    public async Task Sem_a_chave_do_Agent_a_página_nem_carrega_o_módulo()
    {
        await using var semAgent = await ApiDeTeste.SubirAsync(chaveDoAgent: null);

        var (_, html) = await semAgent.GetAsync("/api/docs/");

        Assert.DoesNotContain("aleph.js", html);
        Assert.DoesNotContain(ApiDeTeste.ChaveDaApi, html);
    }

    // ---- módulo que entrega a chave -------------------------------------------

    // origem é só o rótulo que aparece no nome do caso; o que conta são o IP de quem conectou,
    // o X-Forwarded-For (que só vale vindo de dentro) e a marca do Funnel
    [Theory]
    [InlineData("loopback", "127.0.0.1", null, false, true)]
    [InlineData("rede do Docker", "172.18.0.1", null, false, true)]
    [InlineData("Tailscale (X-Forwarded-For da tailnet)", "127.0.0.1", "100.94.84.30", false, true)]
    [InlineData("Funnel", "127.0.0.1", "203.0.113.9", true, false)]
    [InlineData("IP público atrás do proxy", "127.0.0.1", "203.0.113.9", false, false)]
    [InlineData("IP público direto", "203.0.113.9", null, false, false)]
    [InlineData("IP público forjando X-Forwarded-For", "203.0.113.9", "10.0.0.1", false, false)]
    public async Task Módulo_só_leva_a_chave_pra_quem_está_dentro(
        string origem, string ip, string? encaminhadoPor, bool peloFunnel, bool levaAChave)
    {
        List<(string, string)> headers = [];

        if (encaminhadoPor is not null)
            headers.Add(("X-Forwarded-For", encaminhadoPor));

        if (peloFunnel)
            headers.Add(PeloFunnel);

        var (status, corpo) = await _api.GetAsync("/api/docs/aleph.js", IPAddress.Parse(ip), headers: [.. headers]);

        Assert.Equal(200, status);
        Assert.True(
            corpo.Contains(ApiDeTeste.ChaveDaApi) == levaAChave,
            $"pedido vindo de {origem}: {(levaAChave ? "devia" : "não devia")} levar a chave");
    }
}

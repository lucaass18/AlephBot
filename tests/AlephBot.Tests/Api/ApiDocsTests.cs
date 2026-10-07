using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AlephBot.Tests.Api;

/// <summary>
/// Os docs (/api/v1/docs) e o que eles carregam. A regra que importa: de fora (pelo Funnel ou
/// com IP público) ninguém leva chave nenhuma — nem a da API, nem a da IA do Scalar.
/// </summary>
public partial class ApiDocsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly ApiDeTeste _api = fixture.Api;

    private static readonly (string, string) PeloFunnel = ("Tailscale-Funnel-Request", "?1");

    private async Task<JsonNode> DocumentoAsync()
    {
        var (_, corpo) = await _api.GetAsync("/api/v1/openapi.json");
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
    public async Task As_rotas_ficam_em_dois_grupos_com_explicação()
    {
        var documento = await DocumentoAsync();
        var grupos = documento["tags"]!.AsArray();
        var rotas = documento["paths"]!;

        string? GrupoDe(string caminho, string método) =>
            (string?)Assert.Single(rotas[caminho]![método]!["tags"]!.AsArray());

        Assert.Equal(["Status", "Bot"], grupos.Select(g => (string?)g!["name"]));
        Assert.All(grupos, g => Assert.False(string.IsNullOrWhiteSpace((string?)g!["description"])));

        Assert.Equal("Status", GrupoDe("/api/v1/health", "get"));
        Assert.Equal("Status", GrupoDe("/api/v1/health", "head"));
        Assert.Equal("Status", GrupoDe("/api/v1/status", "get"));
        Assert.Equal("Bot", GrupoDe("/api/v1/stats", "get"));
        Assert.Equal("Bot", GrupoDe("/api/v1/commands", "get"));
    }

    [Fact]
    public async Task Os_exemplos_saem_no_mesmo_JSON_das_respostas()
    {
        // camelCase e enum como texto: o exemplo passa pelo mesmo serializador das rotas
        var rotas = (await DocumentoAsync())["paths"]!;

        JsonNode Exemplo(string caminho) =>
            rotas[caminho]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["example"]!;

        var status = Exemplo("/api/v1/status");

        Assert.Equal("AlephBot", (string?)status["bot"]!["username"]);
        Assert.Equal("production", (string?)status["mode"]);
        Assert.Equal("connected", (string?)status["discord"]!["status"]);
        Assert.Equal("ok", (string?)Exemplo("/api/v1/health")["status"]);
        Assert.Equal(12, (int)Exemplo("/api/v1/stats")["guilds"]!);
        Assert.Contains(Exemplo("/api/v1/commands").AsArray(), c => (string?)c!["slash"] == "/ping");
    }

    [Fact]
    public async Task O_HEAD_do_health_tem_nome_próprio_e_sai_sem_corpo()
    {
        var head = (await DocumentoAsync())["paths"]!["/api/v1/health"]!["head"]!;

        Assert.Equal("Se o bot está de pé, sem corpo", (string?)head["summary"]);
        Assert.Null(head["responses"]!["200"]!["content"]);
    }

    [Fact]
    public async Task O_documento_conta_o_limite_de_pedidos_em_toda_rota()
    {
        var rotas = (await DocumentoAsync())["paths"]!.AsObject();

        Assert.All(rotas, rota =>
        {
            var respostas = rota.Value!.AsObject().First().Value!["responses"]!;

            Assert.NotNull(respostas["429"]!["headers"]!["Retry-After"]);
            Assert.NotNull(respostas["200"]!["headers"]!["X-RateLimit-Limit"]);
            Assert.NotNull(respostas["200"]!["headers"]!["X-RateLimit-Remaining"]);
        });
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
            "/api/v1/openapi.json",
            host: "aleph.tailc38add.ts.net",
            headers: [PeloFunnel, ("X-Forwarded-Proto", "https"), ("X-Forwarded-For", "203.0.113.9")]);

        Assert.Equal("https://aleph.tailc38add.ts.net", (string?)JsonNode.Parse(corpo)!["servers"]![0]!["url"]);
    }

    // ---- página --------------------------------------------------------------

    // a configuração do Scalar é o objeto JSON que ocupa uma linha inteira do initialize(...),
    // seja qual for a primeira chave dele
    [GeneratedRegex("""(?<=^\s*)\{".*\}(?=,\s*$)""", RegexOptions.Multiline)]
    private static partial Regex ConfigDaPágina();

    private async Task<string> PáginaAsync(params (string, string)[] headers)
    {
        var (status, html) = await _api.GetAsync("/api/v1/docs/", headers: headers);

        Assert.Equal(200, status);
        return html;
    }

    [Fact]
    public async Task Na_rede_de_casa_a_página_tem_a_IA_e_a_chave_automática()
    {
        var html = await PáginaAsync();
        var config = ConfigDaPágina().Match(html).Value;

        Assert.Contains(ApiDeTeste.ChaveDoAgent, config);
        Assert.Contains("/api/v1/docs/aleph.js", html);
        Assert.Contains("\"persistAuth\":true", config);
    }

    [Fact]
    public async Task A_página_tem_a_cara_do_bot()
    {
        var html = await PáginaAsync();
        var config = JsonNode.Parse(ConfigDaPágina().Match(html).Value)!;

        var tema = (string?)config["customCss"];

        // a prévia do link no Discord lê o HTML que o servidor manda, sem rodar script
        Assert.Contains("""<meta name="theme-color" content="#af87ff">""", html);
        Assert.Equal("/api/v1/docs/aleph.svg", (string?)config["favicon"]);

        // o jeito do Cypress com as cores do bot: abre clara, com a barra lateral no
        // azul-marinho dele e o roxo do Banner (escurecido no branco) de destaque
        Assert.False((bool?)config["darkMode"] ?? false);
        Assert.Contains(".t-doc__sidebar", tema);
        Assert.Contains("--scalar-sidebar-background-1: #1b1e2e", tema);
        Assert.Contains("--scalar-color-accent: #713cdd", tema);
        Assert.Contains("--scalar-color-accent: #af87ff", tema);

        // o que é propaganda do Scalar, e não da API, fica de fora
        Assert.Equal("never", (string?)config["showDeveloperTools"]);
        Assert.True((bool?)config["mcp"]!["disabled"]);
    }

    [Fact]
    public async Task O_ícone_da_aba_é_o_aleph()
    {
        var (status, corpo, headers) = await _api.GetComHeadersAsync("/api/v1/docs/aleph.svg");

        Assert.Equal(200, status);
        Assert.StartsWith("image/svg+xml", headers.ContentType.ToString());
        Assert.Contains("ℵ", corpo);
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
        var (_, html) = await _api.GetAsync("/api/v1/docs/", IPAddress.Parse("203.0.113.9"));

        Assert.DoesNotContain("aleph.js", html);
        Assert.Contains("\"persistAuth\":false", ConfigDaPágina().Match(html).Value);
    }

    [Fact]
    public async Task Sem_a_chave_do_Agent_a_página_nem_carrega_o_módulo()
    {
        await using var semAgent = await ApiDeTeste.SubirAsync(chaveDoAgent: null);

        var (_, html) = await semAgent.GetAsync("/api/v1/docs/");

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

        var (status, corpo) = await _api.GetAsync("/api/v1/docs/aleph.js", IPAddress.Parse(ip), headers: [.. headers]);

        Assert.Equal(200, status);
        Assert.True(
            corpo.Contains(ApiDeTeste.ChaveDaApi) == levaAChave,
            $"pedido vindo de {origem}: {(levaAChave ? "devia" : "não devia")} levar a chave");
    }
}

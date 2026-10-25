using System.Text.Json.Nodes;

namespace AlephBot.Tests.Api;

// O documento OpenAPI: o que ele promete é o contrato que a coleção do Postman e a caixa
// preta cobram.
public class ApiDocsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
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
    public async Task Cada_resposta_diz_o_que_quer_dizer_em_português()
    {
        var rotas = (await DocumentoAsync())["paths"]!;

        string? Significado(string caminho, string código) =>
            (string?)rotas[caminho]!["get"]!["responses"]![código]!["description"];

        Assert.Equal("Deu certo.", Significado("/api/v1/status", "200"));
        Assert.Equal("Sem a chave, ou com ela errada.", Significado("/api/v1/stats", "401"));
        Assert.StartsWith("Passou do limite de pedidos", Significado("/api/v1/commands", "429"));
        Assert.Equal("O bot está fora do Discord (`down`).", Significado("/api/v1/health", "503"));
    }

    [Fact]
    public async Task A_introdução_vem_em_seções()
    {
        var introdução = (string?)(await DocumentoAsync())["info"]!["description"];

        Assert.Contains("## Chave", introdução);
        Assert.Contains("## Limite de pedidos", introdução);
        Assert.Contains("## Erros", introdução);
        Assert.Contains("## Versões", introdução);
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
            headers:
            [
                PeloFunnel, ("X-Forwarded-Proto", "https"), ("X-Forwarded-For", "203.0.113.9"),
                ("X-Api-Key", ApiDeTeste.ChaveDaApi),
            ]);

        Assert.Equal("https://aleph.tailc38add.ts.net", (string?)JsonNode.Parse(corpo)!["servers"]![0]!["url"]);
    }
}

using AlephBot.Tests.Api.CaixaPreta;
using AlephBot.Threnodian.Api;

using Microsoft.AspNetCore.Http;

namespace AlephBot.Tests.Api;

// Sem CORS, a chave em header próprio faz o navegador pedir permissão antes, e o GET nem sai.
public class CorsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string DeOutroSite = "https://scalar.com";

    private readonly ApiDeTeste _api = fixture.Api;

    private Task<RespostaCrua> PedeLicençaAsync(string rota, string header = ApiKeyFilter.Header) =>
        _api.FazAsync(
            HttpMethods.Options,
            rota,
            headers:
            [
                ("Origin", DeOutroSite),
                ("Access-Control-Request-Method", HttpMethods.Get),
                ("Access-Control-Request-Headers", header),
            ]);

    [Theory]
    [InlineData("/api/v1/stats")]
    [InlineData("/api/v1/commands")]
    public async Task O_navegador_ganha_licença_pra_mandar_a_chave(string rota)
    {
        var resposta = await PedeLicençaAsync(rota);

        // 405 aqui é o bug de antes: sem licença, o navegador desiste antes de mandar a chave
        Assert.NotEqual(StatusCodes.Status405MethodNotAllowed, resposta.Status);
        Assert.Equal(DeOutroSite, resposta.Headers["Access-Control-Allow-Origin"]);
        Assert.Contains(
            ApiKeyFilter.Header,
            resposta.Headers["Access-Control-Allow-Headers"].ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_licença_vale_por_um_tempo_pra_não_dobrar_cada_chamada()
    {
        var resposta = await PedeLicençaAsync("/api/v1/stats");

        Assert.Equal(TimeSpan.FromHours(2).TotalSeconds.ToString(), resposta.Headers["Access-Control-Max-Age"]);
    }

    [Fact]
    public async Task Pedir_licença_não_gasta_a_cota_do_minuto()
    {
        // o OPTIONS não leva dado: cobrar por ele seria cobrar duas vezes pela mesma chamada
        var antes = await _api.FazAsync(HttpMethods.Get, "/api/v1/status");
        await PedeLicençaAsync("/api/v1/stats");
        var depois = await _api.FazAsync(HttpMethods.Get, "/api/v1/status");

        var gastos = int.Parse(antes.Headers[LimiteDePedidos.CabeçalhoDaSobra]!)
            - int.Parse(depois.Headers[LimiteDePedidos.CabeçalhoDaSobra]!);

        Assert.Equal(1, gastos);
    }

    [Theory]
    [InlineData("/api/v1/health")]
    [InlineData("/api/v1/status")]
    [InlineData("/api/v1/stats")]
    public async Task A_resposta_deixa_o_navegador_ler_os_headers_do_limite(string rota)
    {
        // sem isto a resposta chega com os headers escondidos, e o documento promete os três
        var resposta = await _api.FazAsync(
            HttpMethods.Get,
            rota,
            headers: [("Origin", DeOutroSite), (ApiKeyFilter.Header, ApiDeTeste.ChaveDaApi)]);

        var lêem = resposta.Headers["Access-Control-Expose-Headers"].ToString();

        Assert.Equal(DeOutroSite, resposta.Headers["Access-Control-Allow-Origin"]);
        Assert.All(
            (string[])[LimiteDePedidos.CabeçalhoDaCota, LimiteDePedidos.CabeçalhoDaSobra, AlephApi.HeaderDeEspera],
            header => Assert.Contains(header, lêem, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https://site-qualquer.com")]
    [InlineData("https://scalar.com.de-mentira.net")]
    [InlineData("http://aleph.ts.net.de-mentira.com")]
    public async Task Página_de_fora_da_lista_não_ganha_licença(string origem)
    {
        var resposta = await _api.FazAsync(
            HttpMethods.Options,
            "/api/v1/stats",
            headers:
            [
                ("Origin", origem),
                ("Access-Control-Request-Method", HttpMethods.Get),
                ("Access-Control-Request-Headers", ApiKeyFilter.Header),
            ]);

        // sem a liberação o navegador não manda o pedido de verdade
        Assert.True(
            string.IsNullOrEmpty(resposta.Headers["Access-Control-Allow-Origin"]),
            $"{origem} não devia ser liberada");
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://aleph.tailc38add.ts.net")]
    [InlineData("http://aleph")]
    [InlineData("https://scalar.com")]
    public async Task As_páginas_de_casa_e_a_do_Scalar_ganham_licença(string origem)
    {
        var resposta = await _api.FazAsync(
            HttpMethods.Get,
            "/api/v1/health",
            headers: ("Origin", origem));

        Assert.Equal(origem, resposta.Headers["Access-Control-Allow-Origin"]);
    }

    [Fact]
    public async Task O_env_acrescenta_página_sem_mexer_em_código()
    {
        const string nova = "https://painel.exemplo.com";

        await using var api = await ApiDeTeste.SubirAsync(
            variáveis: new Dictionary<string, string> { ["API_CORS_ORIGINS"] = $"{nova},https://outra.com" });

        var liberada = await api.FazAsync(HttpMethods.Get, "/api/v1/health", headers: ("Origin", nova));
        var estranha = await api.FazAsync(
            HttpMethods.Get,
            "/api/v1/health",
            headers: ("Origin", "https://nao-listada.com"));

        Assert.Equal(nova, liberada.Headers["Access-Control-Allow-Origin"]);
        Assert.True(string.IsNullOrEmpty(estranha.Headers["Access-Control-Allow-Origin"]));
    }

    [Fact]
    public async Task Um_asterisco_no_env_volta_a_liberar_qualquer_página()
    {
        // a saída de emergência, se um dia faltar uma origem e não der pra esperar deploy
        await using var api = await ApiDeTeste.SubirAsync(
            variáveis: new Dictionary<string, string> { ["API_CORS_ORIGINS"] = "*" });

        var resposta = await api.FazAsync(
            HttpMethods.Get,
            "/api/v1/health",
            headers: ("Origin", "https://site-qualquer.com"));

        Assert.Equal("*", resposta.Headers["Access-Control-Allow-Origin"]);
    }

    [Fact]
    public async Task Com_a_licença_dada_a_chave_passa_e_os_dados_saem()
    {
        // o caminho inteiro do navegador: pede licença, manda a chave, recebe os dados
        var licença = await PedeLicençaAsync("/api/v1/stats");
        var resposta = await _api.FazAsync(
            HttpMethods.Get,
            "/api/v1/stats",
            headers: [("Origin", DeOutroSite), (ApiKeyFilter.Header, ApiDeTeste.ChaveDaApi)]);

        Assert.NotEqual(StatusCodes.Status405MethodNotAllowed, licença.Status);
        Assert.Equal(StatusCodes.Status200OK, resposta.Status);
        Assert.Contains("guilds", resposta.Corpo);
    }
}

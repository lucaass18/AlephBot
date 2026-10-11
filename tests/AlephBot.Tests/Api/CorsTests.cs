using AlephBot.Tests.Api.CaixaPreta;
using AlephBot.Threnodian.Api;

using Microsoft.AspNetCore.Http;

namespace AlephBot.Tests.Api;

/// <summary>
/// O que o navegador precisa pra chamar a API de uma página que não é a dos docs. A chave vai
/// num header próprio, e header próprio faz o navegador pedir permissão antes, num OPTIONS:
/// sem resposta a esse pedido ele desiste, e o GET de verdade nem chega a sair. Era o que
/// deixava /stats e /commands fora do ar pra IA dos docs mesmo com a chave certa — o curl não
/// via nada, porque curl não pede permissão a ninguém.
/// </summary>
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
        Assert.Equal("*", resposta.Headers["Access-Control-Allow-Origin"]);
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
        // o OPTIONS não traz chave nem leva dado: cobrar por ele seria cobrar duas vezes pela
        // mesma chamada, e a cota de fora acabaria na metade
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
        // sem isto a resposta chega, mas com os headers escondidos — e o documento promete os
        // três a quem usa a API
        var resposta = await _api.FazAsync(
            HttpMethods.Get,
            rota,
            headers: [("Origin", DeOutroSite), (ApiKeyFilter.Header, ApiDeTeste.ChaveDaApi)]);

        var lêem = resposta.Headers["Access-Control-Expose-Headers"].ToString();

        Assert.Equal("*", resposta.Headers["Access-Control-Allow-Origin"]);
        Assert.All(
            (string[])[LimiteDePedidos.CabeçalhoDaCota, LimiteDePedidos.CabeçalhoDaSobra, AlephApi.HeaderDeEspera],
            header => Assert.Contains(header, lêem, StringComparison.OrdinalIgnoreCase));
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

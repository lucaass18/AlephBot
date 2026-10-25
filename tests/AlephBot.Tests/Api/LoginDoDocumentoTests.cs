using System.Net;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;

namespace AlephBot.Tests.Api;

// O documento de fora só abre pra quem entrou com a chave; de dentro ele segue aberto.
public class LoginDoDocumentoTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly IPAddress IpDeFora = IPAddress.Parse("203.0.113.88");

    private readonly ApiDeTeste _api = fixture.Api;

    [Theory]
    [InlineData("/api/v1/openapi.json")]
    [InlineData("/api/openapi.json")]
    [InlineData("/api/v2/openapi.json")]
    public async Task De_fora_sem_chave_o_documento_não_abre(string rota)
    {
        var (status, corpo) = await _api.GetAsync(rota, IpDeFora);

        Assert.Equal(401, status);
        Assert.Null(JsonNode.Parse(corpo)!["paths"]);
    }

    [Fact]
    public async Task Pelo_Funnel_conta_como_de_fora_mesmo_vindo_do_localhost()
    {
        var (status, _) = await _api.GetAsync(
            "/api/v1/openapi.json",
            headers: ("Tailscale-Funnel-Request", "?1"));

        Assert.Equal(401, status);
    }

    [Fact]
    public async Task Navegador_de_fora_vai_pra_tela_de_entrar()
    {
        var (status, _, headers) = await _api.GetComHeadersAsync(
            "/api/v1/openapi.json",
            IpDeFora,
            headers: ("Accept", "text/html,application/xhtml+xml"));

        Assert.Equal(302, status);
        Assert.Equal("/api/docs", headers.Location.ToString());
    }

    [Fact]
    public async Task Com_a_chave_no_header_o_documento_abre_de_fora()
    {
        var (status, corpo) = await _api.GetAsync(
            "/api/v1/openapi.json",
            IpDeFora,
            headers: ("X-Api-Key", ApiDeTeste.ChaveDaApi));

        Assert.Equal(200, status);
        Assert.NotNull(JsonNode.Parse(corpo)!["openapi"]);
    }

    [Fact]
    public async Task Com_a_chave_errada_no_header_não_abre()
    {
        var (status, _) = await _api.GetAsync(
            "/api/v1/openapi.json",
            IpDeFora,
            headers: ("X-Api-Key", "chute"));

        Assert.Equal(401, status);
    }

    [Fact]
    public async Task De_fora_a_tela_de_entrar_pede_a_chave()
    {
        var (status, corpo) = await _api.GetAsync("/api/docs", IpDeFora);

        Assert.Equal(200, status);
        Assert.Contains("type=\"password\"", corpo);
    }

    [Fact]
    public async Task Chave_errada_na_tela_é_401_sem_biscoito()
    {
        var resposta = await EntraAsync("chute");

        Assert.Equal(401, resposta.StatusCode);
        Assert.False(resposta.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Quem_entra_com_a_chave_lê_o_documento_pelo_biscoito()
    {
        var resposta = await EntraAsync(ApiDeTeste.ChaveDaApi);

        Assert.Equal(302, resposta.StatusCode);
        Assert.Equal("/api/v1/openapi.json", resposta.Headers.Location.ToString());

        var biscoito = resposta.Headers.SetCookie.ToString();
        Assert.Contains("httponly", biscoito, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api", biscoito, StringComparison.OrdinalIgnoreCase);

        var (status, corpo) = await _api.GetAsync(
            "/api/v1/openapi.json",
            IpDeFora,
            headers: ("Cookie", biscoito.Split(';')[0]));

        Assert.Equal(200, status);
        Assert.NotNull(JsonNode.Parse(corpo)!["openapi"]);
    }

    [Fact]
    public async Task Biscoito_mexido_na_mão_não_vale()
    {
        var (status, _) = await _api.GetAsync(
            "/api/v1/openapi.json",
            IpDeFora,
            headers: ("Cookie", "aleph_docs=inventado"));

        Assert.Equal(401, status);
    }

    [Fact]
    public async Task As_rotas_abertas_seguem_abertas_de_fora()
    {
        var (status, _) = await _api.GetAsync("/api/v1/status", IpDeFora);

        Assert.Equal(200, status);
    }

    private async Task<HttpResponse> EntraAsync(string chave) =>
        (await _api.Servidor.SendAsync(http =>
        {
            http.Request.Method = HttpMethods.Post;
            http.Request.Path = "/api/docs";
            http.Connection.RemoteIpAddress = IpDeFora;
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Body = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes($"chave={Uri.EscapeDataString(chave)}"));
        })).Response;
}

using System.Net;
using System.Text;

using AlephBot.Threnodian.Api;

namespace AlephBot.Tests.Api;

/// <summary>
/// As camadas de quem vem de fora: os headers de proteção e a senha dos docs. Cada teste sobe
/// a própria API: a senha muda de um pra outro, e o limite de pedidos guarda estado.
/// </summary>
public class ProtecaoDeForaTests
{
    private static readonly IPAddress IpDeFora = IPAddress.Parse("203.0.113.77");

    private static readonly (string, string) PeloFunnel = ("Tailscale-Funnel-Request", "?1");

    private static (string, string) Basic(string usuário, string senha) =>
        ("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuário}:{senha}")));

    // ---- headers -------------------------------------------------------------

    [Theory]
    [InlineData("/api/v1/docs/")]
    [InlineData("/api/v1/status")]
    [InlineData("/api/v1/health")]
    public async Task Ninguém_embute_a_página_nem_a_API_em_outro_site(string rota)
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (_, _, headers) = await api.GetComHeadersAsync(rota, IpDeFora);

        Assert.Equal("DENY", headers.XFrameOptions);
        Assert.Equal("frame-ancestors 'none'", headers.ContentSecurityPolicy);
        Assert.Equal("nosniff", headers.XContentTypeOptions);
    }

    [Fact]
    public async Task Até_o_429_sai_com_os_headers_de_proteção()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        for (var i = 0; i < LimiteDePedidos.DeFora; i++)
            await api.GetAsync("/api/v1/status", IpDeFora);

        var (status, _, headers) = await api.GetComHeadersAsync("/api/v1/status", IpDeFora);

        Assert.Equal(429, status);
        Assert.Equal("DENY", headers.XFrameOptions);
    }

    // ---- senha dos docs -------------------------------------------------------

    private const string Senha = "senha-dos-docs";

    [Fact]
    public async Task Sem_senha_configurada_os_docs_abrem_pra_todo_mundo()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (status, _) = await api.GetAsync("/api/v1/docs/", headers: PeloFunnel);

        Assert.Equal(200, status);
    }

    [Fact]
    public async Task Com_senha_de_fora_o_navegador_pede_login()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, corpo, headers) = await api.GetComHeadersAsync("/api/v1/docs/", headers: PeloFunnel);

        Assert.Equal(401, status);
        Assert.StartsWith("Basic", headers.WWWAuthenticate.ToString());
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, corpo);
    }

    [Theory]
    [InlineData("errada")]
    [InlineData("senha-dos-docs ")]
    [InlineData("")]
    public async Task Senha_errada_não_abre(string tentativa)
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, _) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("aleph", tentativa)]);

        Assert.Equal(401, status);
    }

    [Theory]
    [InlineData("aleph")]
    [InlineData("qualquer um")]
    public async Task Senha_certa_abre_com_qualquer_usuário(string usuário)
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, html) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic(usuário, Senha)]);

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);
    }

    [Fact]
    public async Task De_dentro_a_senha_nunca_é_pedida()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, _) = await api.GetAsync("/api/v1/docs/", IPAddress.Parse("100.94.84.30"));

        Assert.Equal(200, status);
    }

    [Theory]
    [InlineData("/API/V1/DOCS/")]
    [InlineData("/api/v1/docs/aleph.js")]
    [InlineData("/api/v1/docs/scalar.js")]
    public async Task A_senha_guarda_a_página_inteira_em_qualquer_caixa(string caminho)
    {
        // a rota não liga pra maiúscula; a senha também não pode ligar
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, _) = await api.GetAsync(caminho, headers: PeloFunnel);

        Assert.Equal(401, status);
    }

    [Fact]
    public async Task A_senha_é_só_dos_docs_e_não_das_rotas_abertas()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, _) = await api.GetAsync("/api/v1/status", headers: PeloFunnel);

        Assert.Equal(200, status);
    }
}

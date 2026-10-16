using System.Net;
using System.Text;

using AlephBot.Threnodian.Api;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Api;

// Os headers de proteção e o crachá de dono dos docs. Cada teste sobe a própria API: há estado.
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
        Assert.Equal("no-referrer", headers["Referrer-Policy"]);
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

    // ---- a documentação, aberta -----------------------------------------------

    private const string SenhaDoDono = "senha-do-dono";

    [Fact]
    public async Task Sem_senha_configurada_os_docs_abrem_pra_todo_mundo()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (status, _) = await api.GetAsync("/api/v1/docs/", headers: PeloFunnel);

        Assert.Equal(200, status);
    }

    [Fact]
    public async Task De_fora_a_página_abre_sem_senha_e_sem_a_IA()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html, headers) = await api.GetComHeadersAsync("/api/v1/docs/", headers: PeloFunnel);

        // ninguém é mandado pra tela de entrada, e a janelinha do navegador não aparece
        Assert.Equal(200, status);
        Assert.Equal("", headers.Location.ToString());
        Assert.Equal("", headers.WWWAuthenticate.ToString());

        // a documentação inteira está lá; só a IA, que é paga, fica de fora
        Assert.Contains("openapi.json", html);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
    }

    [Theory]
    [InlineData("errada")]
    [InlineData("senha-do-dono ")]
    [InlineData("")]
    public async Task Senha_errada_no_header_não_tranca_a_página_nem_traz_a_IA(string tentativa)
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("aleph", tentativa)]);

        Assert.Equal(200, status);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
    }

    [Fact]
    public async Task A_senha_de_dono_não_mexe_nas_rotas_abertas()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, _) = await api.GetAsync("/api/v1/status", headers: PeloFunnel);

        Assert.Equal(200, status);
    }

    // ---- a tela de entrada do dono --------------------------------------------

    [Fact]
    public async Task A_tela_de_entrada_abre_sem_senha_e_não_entrega_nada()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/api/v1/docs/entrar", headers: PeloFunnel);

        Assert.Equal(200, status);
        Assert.Contains("name=\"senha\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
        Assert.DoesNotContain(ApiDeTeste.ChaveDaApi, html);
    }

    [Fact]
    public async Task Sem_senha_configurada_não_há_tela_pedindo_nada()
    {
        // sem a senha de dono não há como entrar: o caminho da tela nem é registrado
        await using var api = await ApiDeTeste.SubirAsync();

        var (status, html) = await api.GetAsync("/api/v1/docs/entrar", headers: PeloFunnel);

        Assert.Equal(200, status);
        Assert.DoesNotContain("type=\"password\"", html);
    }

    // Preenche a tela e manda, como o navegador faria; devolve o biscoito que voltou.
    private static async Task<(int Status, string Html, string? Biscoito)> MandaASenhaAsync(
        ApiDeTeste api, string senha)
    {
        var contexto = await api.Servidor.SendAsync(http =>
        {
            http.Request.Method = Microsoft.AspNetCore.Http.HttpMethods.Post;
            http.Request.Path = "/api/v1/docs/entrar";
            http.Request.Headers["Tailscale-Funnel-Request"] = "?1";

            // o mesmo IP que o GetAsync usa: o castigo conta por IP
            http.Connection.RemoteIpAddress = IPAddress.Loopback;
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Form = new Microsoft.AspNetCore.Http.FormCollection(
                new() { ["senha"] = senha });
        });

        var posto = contexto.Response.Headers.SetCookie.ToString();

        using var leitor = new StreamReader(contexto.Response.Body);

        return (
            contexto.Response.StatusCode,
            await leitor.ReadToEndAsync(),
            posto.Length == 0 ? null : posto.Split(';')[0]);
    }

    [Fact]
    public async Task Quem_preenche_a_tela_recebe_o_biscoito_e_entra_como_dono()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html, biscoito) = await MandaASenhaAsync(api, SenhaDoDono);

        // a tela de "entrou" diz que é como dono, que é o que explica o que tem do outro lado
        Assert.Equal(200, status);
        Assert.Contains("como dono", html);
        Assert.Contains($"href=\"{ApiDocs.Página}/\"", html);
        Assert.NotNull(biscoito);

        // e com ele na mão a página abre com a IA, sem passar pela tela de novo
        var (depois, docs) = await api.GetAsync(
            "/api/v1/docs/",
            headers: [PeloFunnel, ("Cookie", biscoito!)]);

        Assert.Equal(200, depois);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, docs);
    }

    [Fact]
    public async Task Senha_errada_na_tela_não_dá_biscoito()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, _, biscoito) = await MandaASenhaAsync(api, "chutada");

        Assert.Equal(401, status);
        Assert.Null(biscoito);
    }

    [Fact]
    public async Task Biscoito_forjado_não_vale_como_crachá()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync(
            "/api/v1/docs/",
            headers: [PeloFunnel, ("Cookie", "aleph_docs=99999999999")]);

        // sem a assinatura do processo não adianta escrever a validade na mão: abre como visitante
        Assert.Equal(200, status);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
    }

    // ---- castigo de quem chuta senha ------------------------------------------

    [Fact]
    public async Task Depois_de_quatro_erros_nem_a_senha_certa_entra()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        // os três primeiros erros ainda pedem senha de novo
        for (var i = 1; i < TentativasDeSenha.Limite; i++)
        {
            var (status, _, _) = await MandaASenhaAsync(api, $"chute {i}");
            Assert.Equal(401, status);
        }

        // o quarto fecha a porta
        var (quarto, aviso, _) = await MandaASenhaAsync(api, "chute 4");

        Assert.Equal(429, quarto);
        Assert.Contains("Muitas tentativas", aviso);

        // e a senha certa, agora, também não abre
        var (certa, html, biscoito) = await MandaASenhaAsync(api, SenhaDoDono);

        Assert.Equal(429, certa);
        Assert.Contains("Muitas tentativas", html);
        Assert.Null(biscoito);
    }

    [Fact]
    public async Task De_castigo_a_própria_tela_avisa_em_vez_de_pedir_senha()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        for (var i = 1; i <= TentativasDeSenha.Limite; i++)
            await MandaASenhaAsync(api, "chutada");

        var (status, html) = await api.GetAsync("/api/v1/docs/entrar", headers: PeloFunnel);

        Assert.Equal(429, status);
        Assert.Contains("Muitas tentativas", html);
        Assert.DoesNotContain("type=\"password\"", html);
    }

    [Fact]
    public async Task Acertar_antes_do_quarto_erro_zera_a_conta()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        for (var i = 1; i < TentativasDeSenha.Limite; i++)
            await MandaASenhaAsync(api, "quase");

        var (acertou, _, biscoito) = await MandaASenhaAsync(api, SenhaDoDono);

        Assert.Equal(200, acertou);
        Assert.NotNull(biscoito);

        // zerou: dá pra errar de novo as três sem cair no castigo
        for (var i = 1; i < TentativasDeSenha.Limite; i++)
        {
            var (status, _, _) = await MandaASenhaAsync(api, "quase");
            Assert.Equal(401, status);
        }
    }

    [Fact]
    public async Task Errar_a_senha_no_header_também_conta()
    {
        // o chute por curl é o mesmo chute; contar só o formulário deixaria a porta aberta
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        for (var i = 1; i <= TentativasDeSenha.Limite; i++)
            await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("a", $"chute {i}")]);

        var (status, html) = await api.GetAsync("/api/v1/docs/entrar", headers: PeloFunnel);

        Assert.Equal(429, status);
        Assert.Contains("Muitas tentativas", html);
    }

    [Fact]
    public async Task Quem_já_entrou_não_perde_a_IA_pelo_chute_de_outro()
    {
        // o biscoito vale mesmo de castigo: quem está lendo a documentação não está chutando
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (_, _, biscoito) = await MandaASenhaAsync(api, SenhaDoDono);

        for (var i = 1; i <= TentativasDeSenha.Limite; i++)
            await MandaASenhaAsync(api, "chutada");

        var (status, html) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, ("Cookie", biscoito!)]);

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);
    }

    [Fact]
    public void O_castigo_esquece_a_conta_quando_o_tempo_passa()
    {
        var contador = new TentativasDeSenha(NullLogger<TentativasDeSenha>.Instance);
        const string quem = "203.0.113.9";

        for (var i = 1; i <= TentativasDeSenha.Limite; i++)
            contador.Errou(quem, agoraEmMs: 0);

        Assert.NotNull(contador.Falta(quem, agoraEmMs: 0));

        // batendo na porta durante o castigo, ele não recomeça do zero
        contador.Errou(quem, agoraEmMs: 60_000);
        Assert.NotNull(contador.Falta(quem, agoraEmMs: 60_000));

        Assert.Null(contador.Falta(quem, (long)TentativasDeSenha.Castigo.TotalMilliseconds + 1));
    }

    // ---- o crachá de dono, de fora --------------------------------------------

    [Theory]
    [InlineData("aleph")]
    [InlineData("qualquer um")]
    public async Task A_senha_de_dono_vale_com_qualquer_usuário(string usuário)
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic(usuário, SenhaDoDono)]);

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);
    }

    [Fact]
    public async Task Senha_de_dono_traz_a_IA_mas_nunca_a_chave_da_API()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync(
            "/api/v1/docs/",
            headers: [PeloFunnel, Basic("eu", SenhaDoDono)]);

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);

        // a chave da API é mais fechada que a IA: nem pro dono de fora, que cola a dele à mão
        Assert.DoesNotContain(ApiDeTeste.ChaveDaApi, html);
        Assert.DoesNotContain("aleph.js", html);
    }

    [Fact]
    public async Task Sem_a_senha_de_dono_configurada_a_IA_não_sai_de_casa()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (status, html) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("v", SenhaDoDono)]);

        Assert.Equal(200, status);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
    }

    [Fact]
    public async Task O_crachá_vale_em_qualquer_caixa()
    {
        // a rota não liga pra maiúscula; o crachá também não pode ligar
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/API/V1/DOCS/", headers: [PeloFunnel, Basic("eu", SenhaDoDono)]);

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);
    }

    [Fact]
    public async Task De_dentro_continua_dono_sem_senha_nenhuma()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/api/v1/docs/");

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);
        Assert.Contains("aleph.js", html);
    }

    [Fact]
    public async Task Da_tailnet_também_é_dono_sem_senha()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/api/v1/docs/", IPAddress.Parse("100.94.84.30"));

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);
    }
}

using System.Net;
using System.Text;

using AlephBot.Threnodian.Api;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Api;

// Os headers de proteção e a senha dos docs. Cada teste sobe a própria API: há estado.
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
    public async Task De_fora_a_página_manda_pra_tela_de_entrada()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, corpo, headers) = await api.GetComHeadersAsync("/api/v1/docs/", headers: PeloFunnel);

        // a tela é da casa, no lugar da janelinha do navegador: nada de WWW-Authenticate
        Assert.Equal(302, status);
        Assert.Equal("/api/v1/docs/entrar", headers.Location);
        Assert.Equal("", headers.WWWAuthenticate.ToString());
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, corpo);
    }

    [Theory]
    [InlineData("errada")]
    [InlineData("senha-dos-docs ")]
    [InlineData("")]
    public async Task Senha_errada_no_header_não_abre(string tentativa)
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, _) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("aleph", tentativa)]);

        Assert.Equal(302, status);
    }

    // ---- a tela de entrada ----------------------------------------------------

    [Fact]
    public async Task A_tela_de_entrada_abre_sem_senha_e_não_entrega_nada()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/api/v1/docs/entrar", headers: PeloFunnel);

        Assert.Equal(200, status);
        Assert.Contains("name=\"senha\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
        Assert.DoesNotContain(ApiDeTeste.ChaveDaApi, html);
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
    public async Task Quem_preenche_a_tela_recebe_o_biscoito_e_entra()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (status, html, biscoito) = await MandaASenhaAsync(api, SenhaDoDono);

        // a tela de "entrou" diz com que crachá, que é o que explica o que tem do outro lado
        Assert.Equal(200, status);
        Assert.Contains("como dono", html);
        Assert.Contains($"href=\"{ApiDocs.Página}/\"", html);
        Assert.NotNull(biscoito);

        // e com ele na mão a página abre, sem passar pela tela de novo
        var (depois, docs) = await api.GetAsync(
            "/api/v1/docs/",
            headers: [PeloFunnel, ("Cookie", biscoito!)]);

        Assert.Equal(200, depois);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, docs);
    }

    [Fact]
    public async Task O_biscoito_de_visitante_abre_a_página_sem_a_IA()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (_, entrou, biscoito) = await MandaASenhaAsync(api, Senha);
        var (status, html) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, ("Cookie", biscoito!)]);

        // a tela avisa que a IA fica de fora, senão ele procuraria por ela do outro lado
        Assert.Contains("como visitante", entrou);
        Assert.Contains("IA do Scalar fica de fora", entrou);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, entrou);

        Assert.Equal(200, status);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
    }

    [Fact]
    public async Task Senha_errada_na_tela_não_dá_biscoito()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (status, _, biscoito) = await MandaASenhaAsync(api, "chutada");

        Assert.Equal(401, status);
        Assert.Null(biscoito);
    }

    [Fact]
    public async Task Biscoito_forjado_não_vale()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (status, _) = await api.GetAsync(
            "/api/v1/docs/",
            headers: [PeloFunnel, ("Cookie", "aleph_docs=Dono|99999999999")]);

        // sem a assinatura do processo não adianta escrever "Dono" na mão
        Assert.Equal(302, status);
    }

    // ---- castigo de quem chuta senha ------------------------------------------

    [Fact]
    public async Task Depois_de_quatro_erros_nem_a_senha_certa_entra()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

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
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

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
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

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
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        for (var i = 1; i <= TentativasDeSenha.Limite; i++)
            await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("a", $"chute {i}")]);

        var (status, html) = await api.GetAsync("/api/v1/docs/entrar", headers: PeloFunnel);

        Assert.Equal(429, status);
        Assert.Contains("Muitas tentativas", html);
    }

    [Fact]
    public async Task Quem_já_entrou_não_é_castigado_pelo_chute_de_outro()
    {
        // o biscoito vale mesmo de castigo: quem está lendo a documentação não está chutando
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (_, _, biscoito) = await MandaASenhaAsync(api, SenhaDoDono);

        for (var i = 1; i <= TentativasDeSenha.Limite; i++)
            await MandaASenhaAsync(api, "chutada");

        var (status, _) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, ("Cookie", biscoito!)]);

        Assert.Equal(200, status);
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

    [Fact]
    public async Task Sem_senha_configurada_não_há_tela_pedindo_nada()
    {
        // sem senha os docs são abertos: o caminho da tela nem é registrado
        await using var api = await ApiDeTeste.SubirAsync();

        var (status, html) = await api.GetAsync("/api/v1/docs/entrar", headers: PeloFunnel);

        Assert.Equal(200, status);
        Assert.DoesNotContain("type=\"password\"", html);
    }

    [Theory]
    [InlineData("aleph")]
    [InlineData("qualquer um")]
    public async Task Senha_de_visitante_abre_com_qualquer_usuário_mas_sem_a_IA(string usuário)
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, html) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic(usuário, Senha)]);

        // ele lê a documentação inteira e testa o que é aberto; a IA, que é paga, não vem junto
        Assert.Equal(200, status);
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
        Assert.Contains("openapi.json", html);
    }

    // ---- dono de fora ---------------------------------------------------------

    private const string SenhaDoDono = "senha-do-dono";

    [Fact]
    public async Task Senha_de_dono_abre_a_página_inteira_de_fora()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

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
    public async Task As_duas_senhas_convivem_e_cada_uma_abre_o_seu()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (_, doVisitante) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("v", Senha)]);
        var (_, doDono) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("d", SenhaDoDono)]);

        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, doVisitante);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, doDono);
    }

    [Fact]
    public async Task Só_com_a_senha_de_dono_configurada_visitante_nenhum_entra()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (semSenha, _) = await api.GetAsync("/api/v1/docs/", headers: PeloFunnel);
        var (comOutra, _) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("v", Senha)]);
        var (dono, _) = await api.GetAsync("/api/v1/docs/", headers: [PeloFunnel, Basic("d", SenhaDoDono)]);

        Assert.Equal(302, semSenha);
        Assert.Equal(302, comOutra);
        Assert.Equal(200, dono);
    }

    [Fact]
    public async Task De_dentro_continua_dono_sem_senha_nenhuma()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha, senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/api/v1/docs/");

        Assert.Equal(200, status);
        Assert.Contains(ApiDeTeste.ChaveDoAgent, html);
        Assert.Contains("aleph.js", html);
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

        Assert.Equal(302, status);
    }

    [Fact]
    public async Task A_senha_é_só_dos_docs_e_não_das_rotas_abertas()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDosDocs: Senha);

        var (status, _) = await api.GetAsync("/api/v1/status", headers: PeloFunnel);

        Assert.Equal(200, status);
    }
}

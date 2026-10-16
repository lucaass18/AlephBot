using AlephBot.Threnodian.Api;

namespace AlephBot.Tests.Api;

// A raiz do endereço: a tela que pergunta por qual porta entrar.
public class PortaDaFrenteTests
{
    private const string SenhaDoDono = "senha-do-dono";

    private static readonly (string, string) PeloFunnel = ("Tailscale-Funnel-Request", "?1");

    [Fact]
    public async Task A_raiz_pergunta_por_qual_porta_entrar()
    {
        await using var api = await ApiDeTeste.SubirAsync(senhaDoDono: SenhaDoDono);

        var (status, html) = await api.GetAsync("/", headers: PeloFunnel);

        // uma porta pra cada modo: a documentação, que é aberta, e a tela que pede a senha
        Assert.Equal(200, status);
        Assert.Contains($"href=\"{ApiDocs.Página}/\"", html);
        Assert.Contains($"href=\"{ApiDocs.Página}/entrar\"", html);

        // é a porta, não a casa: nem a chave da IA nem a da API aparecem aqui
        Assert.DoesNotContain(ApiDeTeste.ChaveDoAgent, html);
        Assert.DoesNotContain(ApiDeTeste.ChaveDaApi, html);
    }

    [Fact]
    public async Task Sem_senha_de_dono_a_raiz_não_tem_o_que_perguntar()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (status, _, headers) = await api.GetComHeadersAsync("/", headers: PeloFunnel);

        // sem senha não existe porta de dono: sobra a documentação, e ela abre direto
        Assert.Equal(302, status);
        Assert.Equal($"{ApiDocs.Página}/", headers.Location.ToString());
    }
}

using AlephBot.Core.Commands.Anime;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using Microsoft.Extensions.Logging.Abstractions;

using NetCord.Rest;

namespace AlephBot.Tests.MyAnimeList;

// O que o embed do /ma passa a mostrar: o elenco, e a idade do dado quando ele é velho.
public class FichaDoMaTests
{
    private static readonly GoogleTranslator Tradutor = new(NullLogger<GoogleTranslator>.Instance);

    private static readonly GuardaFalsa SemBanco = new() { Disponível = false };

    private static async Task<FichaPronta> FichaDe(Obra obra)
    {
        var ficha = await MyAnimelist.ProcurarAsync(
            new FonteFixa(obra), Tradutor, SemBanco, new SemMangaDex(), obra.Tipo, "naruto", "alguém",
            TestContext.Current.CancellationToken);

        Assert.Null(ficha.Erro);

        return ficha;
    }

    private static async Task<EmbedProperties> EmbedDe(Obra obra) => (await FichaDe(obra)).Embed!;

    private static Obra Ficha(IReadOnlyList<Personagem> elenco, DateTimeOffset? vistoEm = null) => new()
    {
        Tipo = TipoDeObra.Anime,
        Id = 20,
        Título = "naruto",
        Personagens = elenco,
        VistoEm = vistoEm,
    };

    // O elenco saiu do campo e foi pro cartão; sem isto ele teria sumido da ficha.
    [Fact]
    public void O_elenco_entra_no_cartão()
    {
        var elenco = CartãoDaFicha.Elenco(Ficha([
            new Personagem { Nome = "Naruto Uzumaki", Principal = true },
            new Personagem { Nome = "Sasuke Uchiha", Principal = true },
        ]));

        Assert.NotNull(elenco);
        Assert.Contains("Naruto Uzumaki", elenco);
        Assert.Contains("Sasuke Uchiha", elenco);
    }

    // Mangá velho não tem elenco cadastrado; linha vazia é sujeira no cartão.
    [Fact]
    public void Sem_elenco_o_cartão_não_inventa_linha()
    {
        Assert.Null(CartãoDaFicha.Elenco(Ficha([])));
    }

    // O cartão carrega a ficha; o embed vira moldura, sem campo e sem descrição.
    [Fact]
    public async Task A_ficha_vem_com_o_cartão_e_o_embed_fica_de_moldura()
    {
        var ficha = await FichaDe(Ficha([]));

        Assert.NotNull(ficha.Anexo);
        Assert.Null(ficha.Embed!.Fields);
        Assert.Null(ficha.Embed.Description);
        Assert.NotNull(ficha.Embed.Title);
    }

    // Ficha do banco com a fonte fora: nota de uma semana atrás como de agora é mentira.
    [Fact]
    public async Task Ficha_guardada_avisa_de_quando_é()
    {
        var embed = await EmbedDe(Ficha([], vistoEm: new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)));

        Assert.Contains("05/10", embed.Footer!.Text);
    }

    [Fact]
    public async Task Ficha_recém_buscada_não_fala_em_data()
    {
        var embed = await EmbedDe(Ficha([]));

        Assert.DoesNotContain("dados de", embed.Footer!.Text);
    }

    // A capa vem em extraLarge; num selo de 80px no canto ela se perde.
    [Fact]
    public async Task O_embed_aponta_pro_cartão_e_não_pra_capa()
    {
        // porta fechada de propósito: a capa não baixa, e o cartão tem que sair assim mesmo
        var ficha = await FichaDe(Ficha([]) with { Imagem = "https://localhost:1/capa.jpg" });

        Assert.Equal($"attachment://{CartãoDaFicha.Arquivo}", ficha.Embed!.Image?.Url);
        Assert.Null(ficha.Embed.Thumbnail);
        Assert.NotNull(ficha.Anexo);
    }

    // Sinopse inteira empurraria a capa pro fim do rolo; cortada, ela avisa que tem mais.
    [Fact]
    public async Task A_sinopse_vai_inteira_pro_cartão()
    {
        var longa = string.Join(" ", Enumerable.Repeat("Era uma vez uma aventura bem comprida.", 60));

        // já em português: sem isto a ficha passaria pelo tradutor, que é rede
        var ficha = await FichaDe(Ficha([]) with { Sinopse = longa, SinopseEmPortuguês = true });

        // quem corta pra caber é o desenho, e não o texto que chega até ele
        Assert.Equal(longa, ficha.Sinopse);
        Assert.Null(ficha.Embed!.Description);
    }

    private sealed class FonteFixa(Obra obra) : IMyAnimeList
    {
        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Obra>>([obra]);
    }
}

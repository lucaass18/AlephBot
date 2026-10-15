using System.Net;

using AlephBot.Core.Commands.Anime;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.MyAnimeList;

// Título de isekai vem com subtítulo colado e "~" no lugar do ":"; o /ma tenta sem ele.
public class BuscaDoMaTests
{
    private const string Inteiro =
        "Ryuu to Ayumu Nariagari Boukensha-dou ~Youzumi toshite S-Rank Party kara Tsuihou Sareta";

    private const string Começo = "Ryuu to Ayumu Nariagari Boukensha-dou";

    private static readonly GoogleTranslator Tradutor = new(NullLogger<GoogleTranslator>.Instance);

    // Estes testes são sobre a busca, não sobre o cache: aqui o bot roda sem banco.
    private static readonly GuardaFalsa SemBanco = new() { Disponível = false };

    [Fact]
    public async Task Quando_o_título_inteiro_não_acha_tenta_sem_o_subtítulo()
    {
        var fonte = new FonteQueSóSabeOComeço();

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, new SemMangaDex(), TipoDeObra.Manga, Inteiro, "alguém", TestContext.Current.CancellationToken);

        Assert.Null(erro);
        Assert.NotNull(embed);
        Assert.Equal([Inteiro, Começo], fonte.Buscas);
    }

    [Fact]
    public async Task Não_procura_duas_vezes_quando_a_primeira_achou()
    {
        var fonte = new FonteQueSempreAcha();

        await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, new SemMangaDex(), TipoDeObra.Manga, Inteiro, "alguém", TestContext.Current.CancellationToken);

        Assert.Equal([Inteiro], fonte.Buscas);
    }

    // Sem subtítulo não há segunda busca: seria a mesma pergunta duas vezes.
    [Fact]
    public async Task Título_sem_subtítulo_não_vira_duas_buscas()
    {
        var fonte = new FonteQueSóSabeOComeço();

        var (_, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, new SemMangaDex(), TipoDeObra.Anime, "coisa nenhuma", "alguém", TestContext.Current.CancellationToken);

        Assert.NotNull(erro);
        Assert.Equal(["coisa nenhuma"], fonte.Buscas);
    }

    // 403 do AniList é "desliguei a API", não "chave errada": não manda o dono conferir nada.
    [Fact]
    public async Task Fonte_fora_do_ar_com_403_não_fala_em_Client_ID()
    {
        var fonte = new FonteQueFalha(new HttpRequestException("desligada", null, HttpStatusCode.Forbidden));

        var (_, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, new SemMangaDex(), TipoDeObra.Anime, "naruto", "alguém", TestContext.Current.CancellationToken);

        Assert.NotNull(erro);
        Assert.NotEqual(Denia.MalCredencialRecusada(), erro);
    }

    [Fact]
    public async Task Credencial_recusada_manda_conferir_o_Client_ID()
    {
        var fonte = new FonteQueFalha(new CredencialRecusadaException("recusou"));

        var (_, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, new SemMangaDex(), TipoDeObra.Anime, "naruto", "alguém", TestContext.Current.CancellationToken);

        Assert.Equal(Denia.MalCredencialRecusada(), erro);
    }

    [Theory]
    [InlineData("Título ~subtítulo~", "Título")]
    [InlineData("Título: subtítulo", "Título")]
    [InlineData("Título - subtítulo", "Título")]
    [InlineData("Título — subtítulo", "Título")]
    [InlineData("Naruto", null)]
    [InlineData("Fate/stay night", null)]
    [InlineData(": começa no separador", null)]
    [InlineData("ab: curto demais", null)]
    public void Sabe_onde_o_título_acaba_e_o_subtítulo_começa(string busca, string? esperado)
    {
        Assert.Equal(esperado, MyAnimelist.SemSubtítulo(busca));
    }

    private sealed class FonteQueSóSabeOComeço : IMyAnimeList
    {
        public List<string> Buscas { get; } = [];

        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
        {
            Buscas.Add(busca);

            IReadOnlyList<Obra> achados = busca == Começo
                ? [new Obra { Tipo = tipo, Id = 144291, Título = Começo }]
                : [];

            return Task.FromResult(achados);
        }
    }

    private sealed class FonteQueFalha(Exception erro) : IMyAnimeList
    {
        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<Obra>>(erro);
    }

    private sealed class FonteQueSempreAcha : IMyAnimeList
    {
        public List<string> Buscas { get; } = [];

        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
        {
            Buscas.Add(busca);

            IReadOnlyList<Obra> achados = [new Obra { Tipo = tipo, Id = 1, Título = busca }];

            return Task.FromResult(achados);
        }
    }
}

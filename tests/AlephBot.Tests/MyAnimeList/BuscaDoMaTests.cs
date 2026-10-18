using System.Net;

using AlephBot.Core.Commands.Anime;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.MyAnimeList;

// Título de isekai vem com subtítulo colado e "~" no lugar do ":"; o /ma tenta sem ele.
// Na coleção dos emoji porque compara igualdade exata com uma fala da Denia, e as falas
// consultam o mapa de emoji — que é do processo inteiro. Em paralelo, os dois lados da
// comparação pegariam o mapa em momentos diferentes.
[Collection("emoji")]
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

    // O especial do Hawk traz "Nanatsu no Taizai" nos sinônimos e vem antes na busca do AniList;
    // quem se chama assim é o anime, e é ele que a pessoa pediu.
    [Fact]
    public async Task Título_igual_ganha_de_sinônimo_igual()
    {
        var fonte = new FonteEmOrdem(
            new Obra
            {
                Tipo = TipoDeObra.Anime,
                Id = 9574,
                Título = "Hawk no Onayami Soudanshitsu Special",
                Sinônimos = ["Nanatsu no Taizai"],
            },
            new Obra
            {
                Tipo = TipoDeObra.Anime,
                Id = 23755,
                Título = "Nanatsu no Taizai",
                TítuloEmInglês = "The Seven Deadly Sins",
            });

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, mangadex: null, TipoDeObra.Anime, "Nanatsu no taizai", "alguém",
            TestContext.Current.CancellationToken);

        Assert.Null(erro);
        Assert.Contains("Nanatsu no Taizai", embed!.Title!);
        Assert.DoesNotContain("Hawk", embed.Title!);
    }

    // Sinônimo igual continua valendo quando título nenhum bate: "7DS" é a obra, não um apelido solto.
    [Fact]
    public async Task Sinônimo_igual_ganha_de_quem_não_bate_em_nada()
    {
        var fonte = new FonteEmOrdem(
            new Obra { Tipo = TipoDeObra.Anime, Id = 1, Título = "Outra coisa qualquer" },
            new Obra { Tipo = TipoDeObra.Anime, Id = 23755, Título = "Nanatsu no Taizai", Sinônimos = ["7DS"] });

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, mangadex: null, TipoDeObra.Anime, "7ds", "alguém",
            TestContext.Current.CancellationToken);

        Assert.Null(erro);
        Assert.Contains("Nanatsu no Taizai", embed!.Title!);
    }

    // "nanatsu" não é o nome de ninguém: o especial do Hawk ganha da série na ordem do AniList,
    // e entre quem se chama assim de verdade vale a que mais gente acompanha.
    [Fact]
    public async Task Busca_pela_metade_fica_com_a_mais_conhecida()
    {
        var fonte = new FonteEmOrdem(
            new Obra
            {
                Tipo = TipoDeObra.Anime,
                Id = 9574,
                Título = "Hawk no Onayami Soudanshitsu Special",
                Sinônimos = ["Nanatsu no Taizai"],
            },
            new Obra { Tipo = TipoDeObra.Anime, Id = 34618, Título = "Nanatsu no Bitoku" },
            new Obra { Tipo = TipoDeObra.Anime, Id = 23755, Título = "Nanatsu no Taizai", Popularidade = 42 });

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, mangadex: null, TipoDeObra.Anime, "nanatsu", "alguém",
            TestContext.Current.CancellationToken);

        Assert.Null(erro);
        Assert.Contains("Nanatsu no Taizai", embed!.Title!);
    }

    // Pedir o filme não pode virar a série inteira só porque ela tem mais gente na lista.
    [Fact]
    public async Task Filme_pedido_pelo_nome_não_vira_a_série()
    {
        var fonte = new FonteEmOrdem(
            new Obra { Tipo = TipoDeObra.Anime, Id = 1, Título = "ONE PIECE FILM: RED", Popularidade = 98 },
            new Obra { Tipo = TipoDeObra.Anime, Id = 2, Título = "ONE PIECE", Popularidade = 7 });

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, mangadex: null, TipoDeObra.Anime, "one piece film red", "alguém",
            TestContext.Current.CancellationToken);

        Assert.Null(erro);
        Assert.Contains("FILM: RED", embed!.Title!);
    }

    // Ninguém com o nome na cara: fica o primeiro, que é a ordem de relevância da fonte.
    [Fact]
    public async Task Sem_título_parecido_fica_o_primeiro_da_fonte()
    {
        var fonte = new FonteEmOrdem(
            new Obra { Tipo = TipoDeObra.Anime, Id = 1, Título = "NARUTO: Shippuuden", Popularidade = 24 },
            new Obra { Tipo = TipoDeObra.Anime, Id = 2, Título = "NARUTO: Shippuuden Movie", Popularidade = 131 });

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            fonte, Tradutor, SemBanco, mangadex: null, TipoDeObra.Anime, "shippuden", "alguém",
            TestContext.Current.CancellationToken);

        Assert.Null(erro);
        Assert.DoesNotContain("Movie", embed!.Title!);
    }

    // O prazo vale pela busca toda: fonte travada não segura a resposta até alguém desistir.
    [Fact]
    public async Task A_fonte_travada_é_cortada_pelo_prazo()
    {
        using var prazo = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            new FonteQueTrava(), Tradutor, SemBanco, new SemMangaDex(), TipoDeObra.Manga, Inteiro,
            "alguém", prazo.Token);

        Assert.Null(embed);
        Assert.NotNull(erro);
    }

    // As obras na ordem em que a fonte devolveu, que é a ordem em que o AniList ranqueia.
    private sealed class FonteEmOrdem(params Obra[] obras) : IMyAnimeList
    {
        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Obra>>(obras);
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

    // A fonte que aceita o pedido e nunca responde: é assim que uma API fora do ar se parece.
    private sealed class FonteQueTrava : IMyAnimeList
    {
        public string Nome => "Travada";

        public async Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);

            return [];
        }
    }
}

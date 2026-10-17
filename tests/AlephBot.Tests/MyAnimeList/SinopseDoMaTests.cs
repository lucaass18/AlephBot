using AlephBot.Core.Commands.Anime;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using Microsoft.Extensions.Logging.Abstractions;

using NetCord.Rest;

namespace AlephBot.Tests.MyAnimeList;

// A sinopse era sempre tradução de máquina; com o MangaDex confirmando, vale a dele.
public class SinopseDoMaTests
{
    private const string EmPortuguês = "Gol D. Roger escondeu seu tesouro em algum lugar da Grand Line.";

    private static readonly GoogleTranslator Tradutor = new(NullLogger<GoogleTranslator>.Instance);

    private static readonly GuardaFalsa SemBanco = new() { Disponível = false };

    private static Obra Mangá(string? sinopse = "Gol D. Roger was known as the Pirate King.") => new()
    {
        Fonte = "AniList",
        Tipo = TipoDeObra.Manga,
        Id = 13,
        Título = "One Piece",
        Sinopse = sinopse,
    };

    private static Task<FichaPronta> ProcurarAsync(
        Obra obra, ISinopseEmPortuguês mangadex, GuardaFalsa? guarda = null) =>
        MyAnimelist.ProcurarAsync(
            new FonteFixa(obra), Tradutor, guarda ?? SemBanco, mangadex, obra.Tipo, "one piece", "alguém",
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Mangá_ganha_a_sinopse_em_português_do_MangaDex()
    {
        var ficha = await ProcurarAsync(Mangá(), new MangaDexFalso(EmPortuguês));

        Assert.Contains("Grand Line", ficha.Sinopse);
        Assert.DoesNotContain("Pirate King", ficha.Sinopse);
    }

    // Dizer "traduzida por máquina" numa sinopse escrita por gente seria mentira.
    [Fact]
    public async Task O_rodapé_diz_que_a_sinopse_é_do_MangaDex()
    {
        var (embed, _) = await ProcurarAsync(Mangá(), new MangaDexFalso(EmPortuguês));

        Assert.Contains("MangaDex", embed!.Footer!.Text);
        Assert.DoesNotContain("traduzida por máquina", embed.Footer.Text);
    }

    // Não casou a obra, nada muda: continua o caminho de antes.
    [Fact]
    public async Task Sem_casamento_a_ficha_fica_como_estava()
    {
        var (embed, erro) = await ProcurarAsync(Mangá(sinopse: null), new MangaDexFalso(null));

        Assert.Null(erro);
        Assert.NotNull(embed);
        Assert.DoesNotContain("MangaDex", embed.Footer!.Text);
    }

    // O MangaDex não tem anime; perguntar seria gastar requisição à toa.
    [Fact]
    public async Task Anime_não_pergunta_ao_MangaDex()
    {
        var anime = new Obra { Fonte = "AniList", Tipo = TipoDeObra.Anime, Id = 20, Título = "NARUTO" };
        var mangadex = new MangaDexFalso(EmPortuguês);

        await ProcurarAsync(anime, mangadex);

        Assert.Equal(0, mangadex.Chamadas);
    }

    // A sinopse que vai pro banco é a que foi mostrada: nada de perguntar nem traduzir de novo.
    [Fact]
    public async Task A_sinopse_em_português_é_a_que_fica_guardada()
    {
        var guarda = new GuardaFalsa();

        await ProcurarAsync(Mangá(), new MangaDexFalso(EmPortuguês), guarda);

        Assert.Equal(EmPortuguês, guarda.Guardada?.Sinopse);
        Assert.True(guarda.Guardada?.SinopseEmPortuguês);
    }

    private sealed class FonteFixa(Obra obra) : IMyAnimeList
    {
        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Obra>>([obra]);
    }

    private sealed class MangaDexFalso(string? sinopse) : ISinopseEmPortuguês
    {
        public int Chamadas { get; private set; }

        public Task<string?> ParaAsync(Obra obra, CancellationToken cancellationToken = default)
        {
            Chamadas++;

            return Task.FromResult(sinopse);
        }
    }
}

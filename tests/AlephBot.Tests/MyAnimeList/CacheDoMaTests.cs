using AlephBot.Core.Commands.Anime;
using AlephBot.Threnodian.Dados;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.MyAnimeList;

// O /ma guarda a ficha que mostrou: não perguntar duas vezes, e responder se a fonte cair.
public class CacheDoMaTests
{
    private static readonly GoogleTranslator Tradutor = new(NullLogger<GoogleTranslator>.Instance);

    private static Obra Ficha(string título, int id = 20, DateTimeOffset? vistoEm = null) => new()
    {
        Tipo = TipoDeObra.Anime,
        Id = id,
        Título = título,
        VistoEm = vistoEm,
    };

    private static Task<FichaPronta> ProcurarAsync(
        IMyAnimeList fonte, IFichasGuardadas guarda, string busca = "naruto") =>
        MyAnimelist.ProcurarAsync(
            fonte, Tradutor, guarda, new SemMangaDex(), TipoDeObra.Anime, busca, "alguém", TestContext.Current.CancellationToken);

    [Fact]
    public async Task Ficha_recente_no_banco_não_incomoda_a_fonte()
    {
        var fonte = new FonteFalsa([Ficha("NARUTO")]);
        var guarda = new GuardaFalsa { Recente = Ficha("NARUTO", vistoEm: DateTimeOffset.UtcNow) };

        var (embed, erro) = await ProcurarAsync(fonte, guarda);

        Assert.Null(erro);
        Assert.NotNull(embed);
        Assert.Equal(0, fonte.Chamadas);
    }

    [Fact]
    public async Task O_que_veio_da_fonte_é_guardado()
    {
        var fonte = new FonteFalsa([Ficha("NARUTO")]);
        var guarda = new GuardaFalsa();

        await ProcurarAsync(fonte, guarda);

        Assert.Equal("naruto", guarda.BuscaGuardada);
        Assert.Equal("NARUTO", guarda.Guardada?.Título);
    }

    // "naruto" devolve "Boruto" na frente; guardar o primeiro daria amanhã uma ficha errada.
    [Fact]
    public async Task Guarda_a_ficha_que_foi_mostrada_e_não_a_primeira_da_lista()
    {
        var fonte = new FonteFalsa([Ficha("Boruto", id: 34566), Ficha("naruto", id: 20)]);
        var guarda = new GuardaFalsa();

        await ProcurarAsync(fonte, guarda);

        Assert.Equal(20, guarda.Guardada?.Id);
    }

    // Fonte fora do ar com ficha velha no banco: melhor dado de ontem que embed vermelho.
    [Fact]
    public async Task Fonte_caída_responde_do_banco()
    {
        var fonte = new FonteFalsa(new HttpRequestException("caiu"));
        var ontem = DateTimeOffset.UtcNow.AddDays(-1);
        var guarda = new GuardaFalsa { Velha = Ficha("NARUTO", vistoEm: ontem) };

        var (embed, erro) = await ProcurarAsync(fonte, guarda);

        Assert.Null(erro);
        Assert.NotNull(embed);
    }

    [Fact]
    public async Task Fonte_caída_e_banco_vazio_é_o_erro_de_sempre()
    {
        var fonte = new FonteFalsa(new HttpRequestException("caiu"));

        var (embed, erro) = await ProcurarAsync(fonte, new GuardaFalsa());

        Assert.Null(embed);
        Assert.NotNull(erro);
    }

    // Sem Postgres o /ma tem que funcionar igualzinho ao de antes do cache existir.
    [Fact]
    public async Task Sem_banco_a_busca_segue_normal()
    {
        var fonte = new FonteFalsa([Ficha("NARUTO")]);
        var guarda = new GuardaFalsa { Disponível = false };

        var (embed, erro) = await ProcurarAsync(fonte, guarda);

        Assert.Null(erro);
        Assert.NotNull(embed);
        Assert.Equal(1, fonte.Chamadas);
        Assert.Null(guarda.Guardada);
    }

    // Não achar fica anotado, pra próxima busca igual não custar a cadeia de fontes de novo.
    [Fact]
    public async Task Não_achar_fica_anotado()
    {
        var guarda = new GuardaFalsa();

        await ProcurarAsync(new FonteFalsa([]), guarda, "asdhjk");

        Assert.Equal(["asdhjk"], guarda.DeramEmNada);
    }

    // Fonte caída não é "não existe": anotar isso esconderia a obra por um minuto à toa.
    [Fact]
    public async Task Fonte_caída_não_conta_como_não_achei()
    {
        var guarda = new GuardaFalsa();

        await ProcurarAsync(new FonteFalsa(new HttpRequestException("caiu")), guarda);

        Assert.Empty(guarda.DeramEmNada);
    }

    // A segunda busca igual nem chega à fonte: é o limite dela que isto poupa.
    [Fact]
    public async Task Busca_que_não_achou_não_pergunta_de_novo()
    {
        var fonte = new FonteFalsa([]);
        var guarda = new ObrasEmCache(null, null, NullLogger<ObrasEmCache>.Instance);

        await ProcurarAsync(fonte, guarda, "asdhjk");

        var (embed, erro) = await ProcurarAsync(fonte, guarda, "asdhjk");

        Assert.Null(embed);
        Assert.NotNull(erro);
        Assert.Equal(1, fonte.Chamadas);
    }

    private sealed class FonteFalsa : IMyAnimeList
    {
        private readonly IReadOnlyList<Obra> _resposta;
        private readonly Exception? _erro;

        public FonteFalsa(IReadOnlyList<Obra> resposta) => _resposta = resposta;

        public FonteFalsa(Exception erro)
        {
            _resposta = [];
            _erro = erro;
        }

        public int Chamadas { get; private set; }

        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
        {
            Chamadas++;

            return _erro is not null
                ? Task.FromException<IReadOnlyList<Obra>>(_erro)
                : Task.FromResult(_resposta);
        }
    }
}

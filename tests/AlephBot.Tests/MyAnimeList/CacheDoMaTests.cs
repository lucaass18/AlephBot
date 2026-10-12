using AlephBot.Core.Commands.Anime;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.MyAnimeList;

/// <summary>
/// O /ma guarda no banco a ficha que mostrou. Serve pra duas coisas: não perguntar de novo
/// o que já se sabe, e continuar respondendo quando a fonte cair — que foi exatamente o que
/// aconteceu quando o Jikan morreu.
/// </summary>
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

    private static Task<(NetCord.Rest.EmbedProperties? Embed, string? Erro)> ProcurarAsync(
        IMyAnimeList fonte, IFichasGuardadas guarda, string busca = "naruto") =>
        MyAnimelist.ProcurarAsync(
            fonte, Tradutor, guarda, TipoDeObra.Anime, busca, "alguém", TestContext.Current.CancellationToken);

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

    /// <summary>
    /// "naruto" devolve "Boruto" na frente, e o comando escolhe o título exato. Guardar o
    /// primeiro da lista faria o banco responder, amanhã, uma ficha que ninguém viu hoje.
    /// </summary>
    [Fact]
    public async Task Guarda_a_ficha_que_foi_mostrada_e_não_a_primeira_da_lista()
    {
        var fonte = new FonteFalsa([Ficha("Boruto", id: 34566), Ficha("naruto", id: 20)]);
        var guarda = new GuardaFalsa();

        await ProcurarAsync(fonte, guarda);

        Assert.Equal(20, guarda.Guardada?.Id);
    }

    /// <summary>Fonte fora do ar com ficha velha no banco: melhor dado de ontem que embed vermelho.</summary>
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

    /// <summary>Sem Postgres o /ma tem que funcionar igualzinho ao de antes do cache existir.</summary>
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

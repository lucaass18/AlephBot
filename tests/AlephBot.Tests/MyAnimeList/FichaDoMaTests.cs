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

    private static async Task<EmbedProperties> EmbedDe(Obra obra)
    {
        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            new FonteFixa(obra), Tradutor, SemBanco, new SemMangaDex(), obra.Tipo, "naruto", "alguém",
            TestContext.Current.CancellationToken);

        Assert.Null(erro);

        return embed!;
    }

    private static Obra Ficha(IReadOnlyList<Personagem> elenco, DateTimeOffset? vistoEm = null) => new()
    {
        Tipo = TipoDeObra.Anime,
        Id = 20,
        Título = "naruto",
        Personagens = elenco,
        VistoEm = vistoEm,
    };

    private static string? Campo(EmbedProperties embed, string nome) =>
        embed.Fields?.FirstOrDefault(campo => campo.Name == nome)?.Value;

    [Fact]
    public async Task O_embed_mostra_o_elenco_principal()
    {
        var embed = await EmbedDe(Ficha([
            new Personagem { Nome = "Naruto Uzumaki", Principal = true },
            new Personagem { Nome = "Sasuke Uchiha", Principal = true },
        ]));

        var elenco = Campo(embed, Denia.MalCampoElenco);

        Assert.NotNull(elenco);
        Assert.Contains("Naruto Uzumaki", elenco);
        Assert.Contains("Sasuke Uchiha", elenco);
    }

    // Mangá velho não tem elenco cadastrado; campo vazio é sujeira no embed.
    [Fact]
    public async Task Sem_elenco_o_campo_nem_aparece()
    {
        var embed = await EmbedDe(Ficha([]));

        Assert.Null(Campo(embed, Denia.MalCampoElenco));
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

    private sealed class FonteFixa(Obra obra) : IMyAnimeList
    {
        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Obra>>([obra]);
    }
}

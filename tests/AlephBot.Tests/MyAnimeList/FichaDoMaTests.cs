using AlephBot.Core.Commands.Anime;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using Microsoft.Extensions.Logging.Abstractions;

using NetCord.Rest;

namespace AlephBot.Tests.MyAnimeList;

// O que o embed do /ma passa a mostrar: o elenco, e a idade do dado quando ele é velho.
// Na mesma coleção dos emoji porque lê os rótulos, e o mapa deles é do processo inteiro:
// em paralelo, um teste mexendo no mapa mudaria o nome do campo que este aqui procura.
[Collection("emoji")]
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

    // A capa vem em extraLarge; num selo de 80px no canto ela se perde.
    [Fact]
    public async Task A_capa_vai_no_corpo_e_não_no_canto()
    {
        var embed = await EmbedDe(Ficha([]) with { Imagem = "https://img/capa.jpg" });

        Assert.Equal("https://img/capa.jpg", embed.Image?.Url);
        Assert.Null(embed.Thumbnail);
    }

    // Sinopse inteira empurraria a capa pro fim do rolo; cortada, ela avisa que tem mais.
    [Fact]
    public async Task Sinopse_longa_é_cortada_e_o_corte_é_avisado()
    {
        var longa = string.Join(" ", Enumerable.Repeat("Era uma vez uma aventura bem comprida.", 60));

        // já em português: sem isto a ficha passaria pelo tradutor, que é rede
        var embed = await EmbedDe(Ficha([]) with { Sinopse = longa, SinopseEmPortuguês = true });

        Assert.NotNull(embed.Description);
        Assert.True(embed.Description!.Length < longa.Length);
        Assert.Contains(Denia.MalSinopseCortada(), embed.Description);
    }

    private sealed class FonteFixa(Obra obra) : IMyAnimeList
    {
        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Obra>>([obra]);
    }
}

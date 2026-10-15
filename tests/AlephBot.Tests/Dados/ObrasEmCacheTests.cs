using AlephBot.Threnodian.Dados;
using AlephBot.Threnodian.MyAnimeList;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Dados;

// A guarda das fichas do /ma: conveniência, não dependência — sem Postgres o /ma funciona.
public class ObrasEmCacheTests
{
    // fábrica e migração nulas: é o bot subindo sem DATABASE_URL
    private static ObrasEmCache SemBanco() => new(null, null, NullLogger<ObrasEmCache>.Instance);

    [Fact]
    public void Sem_banco_a_guarda_se_declara_indisponível()
    {
        Assert.False(SemBanco().Disponível);
    }

    [Fact]
    public async Task Sem_banco_não_acha_ficha_nenhuma()
    {
        var achada = await SemBanco().AcharAsync(
            TipoDeObra.Anime, "naruto", TimeSpan.FromDays(7), TestContext.Current.CancellationToken);

        Assert.Null(achada);
    }

    // Guardar sem banco não pode explodir: quem chamou está no meio de responder alguém.
    [Fact]
    public async Task Sem_banco_guardar_não_levanta_erro()
    {
        var obra = new Obra { Tipo = TipoDeObra.Anime, Id = 20, Título = "NARUTO" };

        await SemBanco().GuardarAsync("naruto", obra, TestContext.Current.CancellationToken);
    }

    // O termo é a chave: "NARUTO ", "naruto" e "Naruto" são a mesma pergunta.
    [Theory]
    [InlineData("NARUTO", "naruto")]
    [InlineData("  Naruto  ", "naruto")]
    [InlineData("Shingeki no Kyojin", "shingeki no kyojin")]
    [InlineData("Mangá   do    Berserk", "manga do berserk")]
    [InlineData("Fullmetal Alchemist: Brotherhood", "fullmetal alchemist: brotherhood")]
    public void Normaliza_o_que_a_pessoa_digitou(string digitado, string esperado)
    {
        Assert.Equal(esperado, ObrasEmCache.Normaliza(digitado));
    }

    // A linha de procura junta todos os nomes, pra achar pelo inglês, pelo japonês ou sinônimo.
    [Fact]
    public void A_linha_de_procura_junta_todos_os_nomes()
    {
        var obra = new Obra
        {
            Tipo = TipoDeObra.Anime,
            Id = 1,
            Título = "Shingeki no Kyojin",
            TítuloEmInglês = "Attack on Titan",
            TítuloEmJaponês = "進撃の巨人",
            Sinônimos = ["AoT"],
        };

        var procura = ObrasEmCache.LinhaDeProcura(obra);

        Assert.Contains("shingeki no kyojin", procura);
        Assert.Contains("attack on titan", procura);
        Assert.Contains("aot", procura);
        Assert.Contains("進撃の巨人", procura);
    }

    // A ficha volta igualzinha; perder a marca de português manda texto bom pro tradutor.
    [Fact]
    public void Ida_e_volta_preserva_a_ficha()
    {
        var original = new Obra
        {
            Fonte = "MangaDex",
            Tipo = TipoDeObra.Manga,
            Id = 13,
            Título = "One Piece",
            TítuloEmJaponês = "ワンピース",
            Sinônimos = ["Wan Pisu"],
            Formato = "manga",
            Situação = Situação.EmAndamento,
            Capítulos = 1100,
            Volumes = 108,
            Início = new DataParcial(1997, 7, 22),
            Fim = DataParcial.Vazia,
            Nota = 9.2,
            Posição = 1,
            Sinopse = "Gol D. Roger escondeu seu tesouro.",
            SinopseEmPortuguês = true,
            Gêneros = ["Adventure", "Shounen"],
            Autoria = ["Oda Eiichirou"],
            Personagens = [new Personagem { Nome = "Monkey D. Luffy", NomeNativo = "ルフィ", Principal = true }],
        };

        var devolta = ObrasEmCache.Converte(ObrasEmCache.Guardada(original));

        Assert.Equal(original.Título, devolta.Título);
        Assert.Equal(original.Início, devolta.Início);
        Assert.Equal(DataParcial.Vazia, devolta.Fim);
        Assert.Equal(original.Nota, devolta.Nota);
        Assert.Equal(original.Sinopse, devolta.Sinopse);
        Assert.True(devolta.SinopseEmPortuguês);
        Assert.Equal(original.Gêneros, devolta.Gêneros);
        Assert.Equal("Monkey D. Luffy", Assert.Single(devolta.Personagens).Nome);
    }
}

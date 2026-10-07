using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.MyAnimeList;

public class ObraTests
{
    [Theory]
    [InlineData("Finished Airing", Situação.Concluída)]
    [InlineData("finished", Situação.Concluída)]
    [InlineData("Currently Airing", Situação.EmAndamento)]
    [InlineData("currently_publishing", Situação.EmAndamento)]
    [InlineData("Not yet aired", Situação.NãoLançada)]
    [InlineData("not_yet_published", Situação.NãoLançada)]
    [InlineData("On Hiatus", Situação.EmHiato)]
    [InlineData("discontinued", Situação.Cancelada)]
    [InlineData("algo novo", Situação.Desconhecida)]
    [InlineData(null, Situação.Desconhecida)]
    public void As_duas_fontes_viram_a_mesma_situação(string? texto, Situação esperada)
    {
        Assert.Equal(esperada, Obra.LerSituação(texto));
    }

    [Theory]
    [InlineData("TV_Special", "tv special")]
    [InlineData("one-shot", "one shot")]
    [InlineData(" Light Novel ", "light novel")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normaliza_formato(string? texto, string? esperado)
    {
        Assert.Equal(esperado, Obra.Normaliza(texto));
    }

    [Fact]
    public void Link_aponta_pro_MyAnimeList()
    {
        var anime = new Obra { Tipo = TipoDeObra.Anime, Id = 1, Título = "Cowboy Bebop" };
        var mangá = new Obra { Tipo = TipoDeObra.Manga, Id = 2, Título = "Berserk" };

        Assert.Equal("https://myanimelist.net/anime/1", anime.Link);
        Assert.Equal("https://myanimelist.net/manga/2", mangá.Link);
    }
}

/// <summary>Data como o MAL guarda: às vezes só o ano. Completar o resto seria inventar.</summary>
public class DataParcialTests
{
    [Theory]
    [InlineData("2002-10-03", "03/10/2002")]
    [InlineData("2002-10-03T00:00:00+00:00", "03/10/2002")]
    [InlineData("2002-10", "10/2002")]
    [InlineData("2002", "2002")]
    public void Mostra_só_o_que_se_sabe(string texto, string esperado)
    {
        Assert.Equal(esperado, DataParcial.De(texto).ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0000-00-00")]
    [InlineData("abc")]
    public void Sem_ano_é_vazia(string? texto)
    {
        var data = DataParcial.De(texto);

        Assert.True(data.ÉVazia);
        Assert.Equal("?", data.ToString());
    }

    [Fact]
    public void Guarda_cada_parte()
    {
        Assert.Equal(new DataParcial(2002, 10, 3), DataParcial.De("2002-10-03"));
        Assert.Equal(new DataParcial(2002, null, null), DataParcial.De("2002"));
    }
}

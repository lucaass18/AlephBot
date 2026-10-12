using System.Text.Json;

using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.MyAnimeList;

/// <summary>
/// O AniList fala o dialeto dele: nota de 0 a 100, situação em maiúsculas, sinopse com HTML
/// e ranking numa lista só. Aqui é onde isso vira a mesma <see cref="Obra"/> que o MAL dá.
/// </summary>
public class AniListTests
{
    private static readonly JsonElement Anime = JsonDocument.Parse(
        """
        {
          "id": 20,
          "idMal": 20,
          "siteUrl": "https://anilist.co/anime/20",
          "title": { "romaji": "NARUTO", "english": "Naruto", "native": "NARUTO -ナルト-" },
          "synonyms": ["Naruto Shounen Hen"],
          "coverImage": { "extraLarge": "https://img/grande.jpg", "large": "https://img/media.jpg" },
          "format": "TV",
          "status": "FINISHED",
          "episodes": 220,
          "chapters": null,
          "volumes": null,
          "startDate": { "year": 2002, "month": 10, "day": 3 },
          "endDate": { "year": 2007, "month": 2, "day": 8 },
          "averageScore": 80,
          "popularity": 732679,
          "genres": ["Action", "Adventure"],
          "description": "Naruto Uzumaki, um ninja de Konoha.<br><br>\nEle quer ser Hokage. <i>Acredite!</i><br>\n(Source: MAL Rewrite)",
          "studios": { "nodes": [{ "name": "Studio Pierrot" }] },
          "staff": { "edges": [{ "role": "Original Creator", "node": { "name": { "full": "Masashi Kishimoto" } } }] },
          "rankings": [
            { "rank": 317, "type": "RATED", "allTime": true },
            { "rank": 12, "type": "POPULAR", "allTime": true },
            { "rank": 3, "type": "RATED", "allTime": false }
          ]
        }
        """).RootElement;

    private static readonly JsonElement Mangá = JsonDocument.Parse(
        """
        {
          "id": 30002,
          "idMal": 2,
          "siteUrl": "https://anilist.co/manga/30002",
          "title": { "romaji": "Berserk", "english": null, "native": "ベルセルク" },
          "synonyms": [],
          "coverImage": { "extraLarge": null, "large": "https://img/media.jpg" },
          "format": "MANGA",
          "status": "RELEASING",
          "episodes": null,
          "chapters": 374,
          "volumes": 41,
          "startDate": { "year": 1989, "month": 8, "day": null },
          "endDate": { "year": null, "month": null, "day": null },
          "averageScore": 93,
          "genres": ["Action"],
          "description": "Guts, o Espadachim Negro.",
          "studios": { "nodes": [] },
          "staff": { "edges": [
            { "role": "Story & Art", "node": { "name": { "full": "Kentarou Miura" } } },
            { "role": "Assistant", "node": { "name": { "full": "Alguem" } } }
          ] },
          "rankings": []
        }
        """).RootElement;

    [Fact]
    public void Lê_um_anime_do_jeito_que_o_embed_espera()
    {
        var obra = AniList.Ler(TipoDeObra.Anime, Anime);

        Assert.NotNull(obra);
        Assert.Equal("NARUTO", obra.Título);
        Assert.Equal("Naruto", obra.TítuloEmInglês);
        Assert.Equal("NARUTO -ナルト-", obra.TítuloEmJaponês);
        Assert.Equal(["Naruto Shounen Hen"], obra.Sinônimos);
        Assert.Equal("https://img/grande.jpg", obra.Imagem);
        Assert.Equal("tv", obra.Formato);
        Assert.Equal(Situação.Concluída, obra.Situação);
        Assert.Equal(220, obra.Episódios);
        Assert.Equal(new DataParcial(2002, 10, 3), obra.Início);
        Assert.Equal(new DataParcial(2007, 2, 8), obra.Fim);
        Assert.Equal(["Action", "Adventure"], obra.Gêneros);
        Assert.Equal(["Studio Pierrot"], obra.Autoria);
    }

    /// <summary>O AniList dá a nota de 0 a 100; o embed mostra de 0 a 10, como o MAL.</summary>
    [Fact]
    public void Nota_vira_a_escala_do_MAL()
    {
        Assert.Equal(8.0, AniList.Ler(TipoDeObra.Anime, Anime)!.Nota);
        Assert.Equal(9.3, AniList.Ler(TipoDeObra.Manga, Mangá)!.Nota);
    }

    /// <summary>
    /// "popularity" no AniList é quanta gente tem a obra na lista, não a posição. A posição
    /// está em "rankings", e só a de todos os tempos compara com a do MAL.
    /// </summary>
    [Fact]
    public void Ranking_sai_da_lista_de_rankings_e_só_o_de_todos_os_tempos()
    {
        var obra = AniList.Ler(TipoDeObra.Anime, Anime)!;

        Assert.Equal(317, obra.Posição);
        Assert.Equal(12, obra.Popularidade);
    }

    [Fact]
    public void Sem_ranking_a_obra_fica_sem_posição()
    {
        var obra = AniList.Ler(TipoDeObra.Manga, Mangá)!;

        Assert.Null(obra.Posição);
        Assert.Null(obra.Popularidade);
    }

    /// <summary>O link é o do MAL quando a obra existe lá: o comando promete MyAnimeList.</summary>
    [Fact]
    public void Link_aponta_pro_MAL_quando_a_obra_tem_id_lá()
    {
        Assert.Equal("https://myanimelist.net/anime/20", AniList.Ler(TipoDeObra.Anime, Anime)!.Link);
    }

    [Fact]
    public void Sem_id_no_MAL_o_link_é_o_do_AniList()
    {
        var sem = JsonDocument.Parse(
            """
            {
              "id": 999,
              "idMal": null,
              "siteUrl": "https://anilist.co/manga/999",
              "title": { "romaji": "So no AniList" },
              "status": "FINISHED"
            }
            """).RootElement;

        var obra = AniList.Ler(TipoDeObra.Manga, sem);

        Assert.NotNull(obra);
        Assert.Equal("https://anilist.co/manga/999", obra.Link);
    }

    /// <summary>A sinopse do AniList vem com HTML e assinada; nada disso é parte da história.</summary>
    [Fact]
    public void Sinopse_perde_o_HTML_e_a_assinatura()
    {
        var sinopse = AniList.Ler(TipoDeObra.Anime, Anime)!.Sinopse;

        Assert.NotNull(sinopse);
        Assert.DoesNotContain("<", sinopse);
        Assert.DoesNotContain("Source:", sinopse);
        Assert.Contains("Naruto Uzumaki, um ninja de Konoha.", sinopse);
        Assert.Contains("\n\n", sinopse);
        Assert.EndsWith("Acredite!", sinopse);
    }

    /// <summary>Mangá não tem estúdio: quem assina é quem escreveu e desenhou, não o assistente.</summary>
    [Fact]
    public void Autoria_de_mangá_sai_do_staff_pelo_papel()
    {
        var obra = AniList.Ler(TipoDeObra.Manga, Mangá)!;

        Assert.Equal(["Kentarou Miura"], obra.Autoria);
        Assert.Equal(374, obra.Capítulos);
        Assert.Equal(41, obra.Volumes);
        Assert.Null(obra.Episódios);
    }

    /// <summary>
    /// O staff de mangá publicado lá fora vem cheio: tradutor, supervisor, quem fez o
    /// letreiro. "Touch-Up Art" tem "art" no nome e não é autoria nenhuma — o que vale é o
    /// papel inteiro, e o "(vols 1-41)" que o AniList pendura nele não muda isso.
    /// </summary>
    [Fact]
    public void Autoria_de_mangá_ignora_quem_só_ajudou()
    {
        var json = JsonDocument.Parse(
            """
            {
              "id": 30002,
              "idMal": 2,
              "title": { "romaji": "Berserk" },
              "staff": { "edges": [
                { "role": "Story & Art (vols 1-41)", "node": { "name": { "full": "Kentarou Miura" } } },
                { "role": "Supervisor (vols 41- )", "node": { "name": { "full": "Kouji Mori" } } },
                { "role": "Story & Art (vols 41- )", "node": { "name": { "full": "Studio Gaga" } } },
                { "role": "Touch-Up Art & Lettering", "node": { "name": { "full": "Dan Nakrosis" } } },
                { "role": "Translator (Spanish)", "node": { "name": { "full": "Marc Bernabe" } } }
              ] }
            }
            """).RootElement;

        Assert.Equal(["Kentarou Miura", "Studio Gaga"], AniList.Ler(TipoDeObra.Manga, json)!.Autoria);
    }

    [Theory]
    [InlineData("FINISHED", Situação.Concluída)]
    [InlineData("RELEASING", Situação.EmAndamento)]
    [InlineData("NOT_YET_RELEASED", Situação.NãoLançada)]
    [InlineData("CANCELLED", Situação.Cancelada)]
    [InlineData("HIATUS", Situação.EmHiato)]
    [InlineData("ALGO_NOVO", Situação.Desconhecida)]
    public void Situação_do_AniList_vira_a_minha(string status, Situação esperada)
    {
        var json = JsonDocument.Parse(
            $$"""
            { "id": 1, "idMal": 1, "title": { "romaji": "x" }, "status": "{{status}}" }
            """).RootElement;

        Assert.Equal(esperada, AniList.Ler(TipoDeObra.Anime, json)!.Situação);
    }

    /// <summary>Sem título não é obra; é lixo que não pode virar embed.</summary>
    [Fact]
    public void Sem_título_não_vira_obra()
    {
        var json = JsonDocument.Parse("""{ "id": 1, "idMal": 1, "title": { "romaji": null } }""").RootElement;

        Assert.Null(AniList.Ler(TipoDeObra.Anime, json));
    }

    /// <summary>O rodapé do embed diz de onde vieram os dados, e aqui não vieram do MAL.</summary>
    [Fact]
    public void A_obra_sabe_dizer_que_veio_do_AniList()
    {
        Assert.Equal("AniList", AniList.Ler(TipoDeObra.Anime, Anime)!.Fonte);
    }
}

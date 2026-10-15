using System.Text.Json;

using AlephBot.Threnodian.MangáDex;
using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.MyAnimeList;

// O MangaDex fala outro dialeto; está aqui pela sinopse em português escrita por gente.
public class MangaDexTests
{
    private static readonly JsonElement OnePiece = JsonDocument.Parse(
        """
        {
          "id": "a1c7c817-4e59-43b7-9365-09675a149a6f",
          "type": "manga",
          "attributes": {
            "title": { "ja-ro": "One Piece" },
            "altTitles": [
              { "ja": "ワンピース" },
              { "en": "One Piece" },
              { "ru": "Ван Пис" }
            ],
            "description": {
              "en": "Gol D. Roger was known as the Pirate King.",
              "pt-br": "One Piece começa quando Gol D. Roger, o Rei dos Piratas, diz que escondeu seu tesouro."
            },
            "links": { "mal": "13", "al": "30013" },
            "originalLanguage": "ja",
            "lastVolume": "",
            "lastChapter": "",
            "publicationDemographic": "shounen",
            "status": "ongoing",
            "year": 1997,
            "contentRating": "suggestive",
            "tags": [
              { "attributes": { "name": { "en": "Award Winning" }, "group": "format" } },
              { "attributes": { "name": { "en": "Adventure" }, "group": "genre" } },
              { "attributes": { "name": { "en": "Monsters" }, "group": "theme" } }
            ]
          },
          "relationships": [
            { "type": "author", "id": "b6045e2c", "attributes": { "name": "Oda Eiichirou" } },
            { "type": "artist", "id": "b6045e2c", "attributes": { "name": "Oda Eiichirou" } },
            { "type": "cover_art", "id": "38383930", "attributes": { "fileName": "2f4aca53.png" } },
            { "type": "manga", "id": "00538a07" },
            { "type": "manga", "id": "064cbe11" }
          ]
        }
        """).RootElement;

    [Fact]
    public void Lê_um_mangá_do_jeito_que_o_embed_espera()
    {
        var obra = MangaDex.Ler(OnePiece);

        Assert.NotNull(obra);
        Assert.Equal(TipoDeObra.Manga, obra.Tipo);
        Assert.Equal("One Piece", obra.Título);
        Assert.Equal("ワンピース", obra.TítuloEmJaponês);
        Assert.Equal(Situação.EmAndamento, obra.Situação);
        Assert.Equal(new DataParcial(1997, null, null), obra.Início);
        Assert.Equal("MangaDex", obra.Fonte);
    }

    // O id do MAL mantém o link do embed indo pra ficha de sempre.
    [Fact]
    public void Link_aponta_pro_MAL_quando_eles_sabem_o_id()
    {
        Assert.Equal("https://myanimelist.net/manga/13", MangaDex.Ler(OnePiece)!.Link);
    }

    [Fact]
    public void Sem_id_no_MAL_o_link_é_o_do_MangaDex()
    {
        var sem = JsonDocument.Parse(
            """
            {
              "id": "abc-123",
              "attributes": {
                "title": { "en": "Só no MangaDex" },
                "description": {},
                "links": {},
                "status": "completed"
              },
              "relationships": []
            }
            """).RootElement;

        Assert.Equal("https://mangadex.org/title/abc-123", MangaDex.Ler(sem)!.Link);
    }

    // É por isto que o MangaDex entrou: sinopse em português escrita por gente.
    [Fact]
    public void A_sinopse_vem_em_português_quando_existe()
    {
        Assert.StartsWith("One Piece começa quando", MangaDex.Ler(OnePiece)!.Sinopse);
    }

    [Fact]
    public void Sem_português_a_sinopse_fica_em_inglês()
    {
        var só_inglês = JsonDocument.Parse(
            """
            {
              "id": "abc-123",
              "attributes": {
                "title": { "en": "Coisa" },
                "description": { "en": "Only in English." },
                "links": {},
                "status": "completed"
              },
              "relationships": []
            }
            """).RootElement;

        Assert.Equal("Only in English.", MangaDex.Ler(só_inglês)!.Sinopse);
    }

    // Autor e desenhista costumam ser a mesma pessoa, e o embed não repete o nome.
    [Fact]
    public void Autoria_sai_dos_relacionamentos_sem_repetir()
    {
        Assert.Equal(["Oda Eiichirou"], MangaDex.Ler(OnePiece)!.Autoria);
    }

    [Fact]
    public void A_capa_é_montada_com_o_id_e_o_arquivo()
    {
        Assert.Equal(
            "https://uploads.mangadex.org/covers/a1c7c817-4e59-43b7-9365-09675a149a6f/2f4aca53.png.512.jpg",
            MangaDex.Ler(OnePiece)!.Imagem);
    }

    // "Award Winning" é etiqueta de formato, não gênero; demografia é como o MAL mostra.
    [Fact]
    public void Gêneros_pegam_gênero_e_tema_mais_a_demografia()
    {
        var gêneros = MangaDex.Ler(OnePiece)!.Gêneros;

        Assert.Contains("Adventure", gêneros);
        Assert.Contains("Monsters", gêneros);
        Assert.Contains("Shounen", gêneros);
        Assert.DoesNotContain("Award Winning", gêneros);
    }

    // Mangá é japonês; coreano é manhwa e chinês é manhua, e o leitor sabe a diferença.
    [Theory]
    [InlineData("ja", "manga")]
    [InlineData("ko", "manhwa")]
    [InlineData("zh", "manhua")]
    [InlineData("zh-hk", "manhua")]
    [InlineData("pt-br", "manga")]
    public void O_formato_vem_do_idioma_original(string idioma, string esperado)
    {
        var json = JsonDocument.Parse(
            $$"""
            {
              "id": "abc-123",
              "attributes": {
                "title": { "en": "Coisa" },
                "description": {},
                "links": {},
                "status": "completed",
                "originalLanguage": "{{idioma}}"
              },
              "relationships": []
            }
            """).RootElement;

        Assert.Equal(esperado, MangaDex.Ler(json)!.Formato);
    }

    [Theory]
    [InlineData("ongoing", Situação.EmAndamento)]
    [InlineData("completed", Situação.Concluída)]
    [InlineData("hiatus", Situação.EmHiato)]
    [InlineData("cancelled", Situação.Cancelada)]
    [InlineData("coisa nova", Situação.Desconhecida)]
    public void Situação_do_MangaDex_vira_a_minha(string status, Situação esperada)
    {
        var json = JsonDocument.Parse(
            $$"""
            {
              "id": "abc-123",
              "attributes": {
                "title": { "en": "Coisa" },
                "description": {},
                "links": {},
                "status": "{{status}}"
              },
              "relationships": []
            }
            """).RootElement;

        Assert.Equal(esperada, MangaDex.Ler(json)!.Situação);
    }

    // Obra em andamento vem com capítulo e volume vazios; zero capítulos seria mentira.
    [Fact]
    public void Capítulo_e_volume_vazios_não_viram_zero()
    {
        var obra = MangaDex.Ler(OnePiece)!;

        Assert.Null(obra.Capítulos);
        Assert.Null(obra.Volumes);
    }

    [Fact]
    public void Capítulo_e_volume_entram_quando_são_número()
    {
        var json = JsonDocument.Parse(
            """
            {
              "id": "abc-123",
              "attributes": {
                "title": { "en": "Coisa" },
                "description": {},
                "links": {},
                "status": "completed",
                "lastVolume": "41",
                "lastChapter": "364"
              },
              "relationships": []
            }
            """).RootElement;

        var obra = MangaDex.Ler(json)!;

        Assert.Equal(364, obra.Capítulos);
        Assert.Equal(41, obra.Volumes);
    }

    [Fact]
    public void Sem_título_não_vira_obra()
    {
        var json = JsonDocument.Parse(
            """{ "id": "abc-123", "attributes": { "title": {}, "links": {} }, "relationships": [] }""").RootElement;

        Assert.Null(MangaDex.Ler(json));
    }

    // O MangaDex não tem anime. Perguntar seria gastar requisição pra ouvir "não tenho".
    [Fact]
    public async Task Busca_de_anime_nem_sai_da_máquina()
    {
        using var mangadex = new MangaDex();

        var achados = await mangadex.ProcurarAsync(
            TipoDeObra.Anime, "naruto", TestContext.Current.CancellationToken);

        Assert.Empty(achados);
    }

    // A busca não traz nota: sem /statistics o embed dizia que ninguém votou em obra popular.
    [Fact]
    public void Lê_as_notas_da_resposta_de_estatísticas()
    {
        var json = JsonDocument.Parse(
            """
            {
              "result": "ok",
              "statistics": {
                "801513ba-a712-498c-8f57-cae55b38cc92": {
                  "follows": 214795,
                  "rating": { "average": 9.6944, "bayesian": 9.688376946778083 }
                },
                "a1c7c817-4e59-43b7-9365-09675a149a6f": {
                  "follows": 141390,
                  "rating": { "average": 9.5624, "bayesian": 9.554036539311955 }
                }
              }
            }
            """).RootElement;

        var notas = MangaDex.LerNotas(json);

        // a bayesiana é a que o site deles mostra, e é a que compara com a nota do MAL
        Assert.Equal(9.688376946778083, notas["801513ba-a712-498c-8f57-cae55b38cc92"]);
        Assert.Equal(9.554036539311955, notas["a1c7c817-4e59-43b7-9365-09675a149a6f"]);
    }

    // Obra sem nota nenhuma não pode virar zero: zero seria uma nota péssima, não "não sei".
    [Fact]
    public void Obra_sem_nota_fica_de_fora_do_mapa()
    {
        var json = JsonDocument.Parse(
            """
            {
              "statistics": {
                "sem-nota": { "follows": 3, "rating": { "average": null, "bayesian": null } },
                "sem-rating": { "follows": 1 }
              }
            }
            """).RootElement;

        Assert.Empty(MangaDex.LerNotas(json));
    }

    // Sem a bayesiana vale a média simples: melhor a nota que eles têm do que nenhuma.
    [Fact]
    public void Sem_a_bayesiana_vale_a_média()
    {
        var json = JsonDocument.Parse(
            """{ "statistics": { "x": { "rating": { "average": 7.5 } } } }""").RootElement;

        Assert.Equal(7.5, MangaDex.LerNotas(json)["x"]);
    }
}

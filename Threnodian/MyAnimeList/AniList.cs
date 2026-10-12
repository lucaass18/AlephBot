using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlephBot.Threnodian.MyAnimeList;

/// <summary>
/// O AniList (graphql.anilist.co): outro catálogo de anime e mangá, aberto e sem chave. É a
/// reserva do /ma desde que o Jikan foi descontinuado — e dá pra ser reserva honesta porque
/// guarda o id da obra no MAL, então o link do embed continua indo pro MyAnimeList.
/// </summary>
public sealed class AniList : IMyAnimeList, IDisposable
{
    private const string Endereço = "https://graphql.anilist.co";

    /// <summary>Quantos resultados eu peço: o primeiro quase sempre serve, os outros são pra achar o título exato.</summary>
    private const int Limite = 5;

    /// <summary>
    /// Uma consulta só, com o tipo e a busca como variáveis. isAdult: false porque isto
    /// responde dentro de servidor de Discord; SEARCH_MATCH porque é a ordem que o AniList
    /// usa na busca do site, e é a que põe o título certo na frente.
    /// </summary>
    private const string Consulta =
        """
        query ($busca: String, $tipo: MediaType, $limite: Int) {
          Page(perPage: $limite) {
            media(search: $busca, type: $tipo, isAdult: false, sort: SEARCH_MATCH) {
              id
              idMal
              siteUrl
              title { romaji english native }
              synonyms
              coverImage { extraLarge large }
              format
              status
              episodes
              chapters
              volumes
              startDate { year month day }
              endDate { year month day }
              averageScore
              genres
              description
              studios(isMain: true) { nodes { name } }
              staff { edges { role node { name { full } } } }
              rankings { rank type allTime }

              # o elenco é subcampo da obra: vem no mesmo pedido, sem custar requisição
              characters(sort: ROLE, perPage: 10) {
                edges {
                  role
                  node {
                    name { full native alternative }
                    image { large }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>O AniList escreve a sinopse com HTML dentro: &lt;br&gt; é parágrafo, &lt;i&gt; é ênfase.</summary>
    private static readonly Regex Quebra = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Etiqueta = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>"(Source: MAL Rewrite)" no fim da sinopse é crédito, não é a história.</summary>
    private static readonly Regex Crédito = new(@"\s*\((?:Source|Fonte):[^)]*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParágrafoDemais = new(@"\n{3,}", RegexOptions.Compiled);

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Endereço),
        Timeout = TimeSpan.FromSeconds(15),
    };

    public string Nome => "AniList";

    public async Task<IReadOnlyList<Obra>> ProcurarAsync(
        TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
    {
        var pergunta = JsonSerializer.Serialize(new
        {
            query = Consulta,
            variables = new
            {
                busca,
                tipo = tipo == TipoDeObra.Anime ? "ANIME" : "MANGA",
                limite = Limite,
            },
        });

        using var corpo = new StringContent(pergunta, Encoding.UTF8, "application/json");
        using var resposta = await _http.PostAsync(string.Empty, corpo, cancellationToken);

        resposta.EnsureSuccessStatusCode();

        await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);

        // GraphQL responde 200 mesmo quando recusa a consulta; o erro vem no corpo
        if (json.RootElement.Campo("errors") is { ValueKind: JsonValueKind.Array } erros && erros.GetArrayLength() > 0)
            throw new JsonException($"O AniList recusou a consulta: {erros[0].Texto("message")}");

        return (json.RootElement.Campo("data")?.Campo("Page")?.Lista("media") ?? [])
            .Select(media => Ler(tipo, media))
            .OfType<Obra>()
            .ToList();
    }

    /// <summary>Um "media" do AniList virando a mesma Obra que o MAL dá; null se não der pra mostrar.</summary>
    internal static Obra? Ler(TipoDeObra tipo, JsonElement media)
    {
        var títulos = media.Campo("title");

        if (Título(títulos) is not { } título)
            return null;

        // o id do MAL é o que mantém o link apontando pro MyAnimeList; sem ele, fica o do AniList
        var noMal = media.Contagem("idMal");
        var própria = media.Texto("siteUrl");

        if (noMal is null && própria is null)
            return null;

        var éAnime = tipo == TipoDeObra.Anime;

        return new Obra
        {
            Fonte = "AniList",
            Tipo = tipo,
            Id = noMal ?? media.Contagem("id") ?? 0,
            Endereço = noMal is null ? própria : null,
            Título = título,
            TítuloEmInglês = títulos?.Texto("english"),
            TítuloEmJaponês = títulos?.Texto("native"),
            Sinônimos = media.Textos("synonyms"),
            Imagem = media.Campo("coverImage") is { } capa
                ? capa.Texto("extraLarge") ?? capa.Texto("large")
                : null,
            Formato = Obra.Normaliza(media.Texto("format")),
            Situação = LerSituação(media.Texto("status")),
            Episódios = éAnime ? media.Contagem("episodes") : null,
            Capítulos = éAnime ? null : media.Contagem("chapters"),
            Volumes = éAnime ? null : media.Contagem("volumes"),
            Início = Data(media.Campo("startDate")),
            Fim = Data(media.Campo("endDate")),

            // o AniList dá a nota de 0 a 100; o embed mostra de 0 a 10, como o MAL
            Nota = media.Decimal("averageScore") / 10,
            Votos = null,
            Posição = Ranking(media, "RATED"),
            Popularidade = Ranking(media, "POPULAR"),
            Sinopse = SemHtml(media.Texto("description")),
            Gêneros = media.Textos("genres"),
            Autoria = éAnime
                ? media.Campo("studios")?.Nomes("nodes") ?? []
                : Autores(media),

            // classificação indicativa é coisa do MAL; o AniList só separa adulto de não adulto
            Classificação = null,

            Personagens = Elenco(media),
        };
    }

    /// <summary>
    /// O elenco na ordem que veio (o sort: ROLE põe protagonista na frente). O nome que o
    /// AniList marca como spoiler fica de fora: numa ficha de busca ele não informa, estraga.
    /// </summary>
    private static List<Personagem> Elenco(JsonElement media)
    {
        var elenco = new List<Personagem>();

        foreach (var papel in media.Campo("characters")?.Lista("edges") ?? [])
        {
            if (papel.Campo("node") is not { } quem || quem.Campo("name")?.Texto("full") is not { } nome)
                continue;

            elenco.Add(new Personagem
            {
                Nome = nome,
                NomeNativo = quem.Campo("name")?.Texto("native"),
                OutrosNomes = quem.Campo("name")?.Textos("alternative") ?? [],
                Principal = string.Equals(papel.Texto("role"), "MAIN", StringComparison.Ordinal),
                Imagem = quem.Campo("image")?.Texto("large"),
            });
        }

        return elenco;
    }

    private static string? Título(JsonElement? títulos) =>
        títulos?.Texto("romaji") ?? títulos?.Texto("english") ?? títulos?.Texto("native");

    /// <summary>"FINISHED", "RELEASING", "NOT_YET_RELEASED": o vocabulário é do AniList, a enum é minha.</summary>
    private static Situação LerSituação(string? texto) => Obra.Normaliza(texto) switch
    {
        "finished" => Situação.Concluída,
        "releasing" => Situação.EmAndamento,
        "not yet released" => Situação.NãoLançada,
        "cancelled" => Situação.Cancelada,
        "hiatus" => Situação.EmHiato,
        _ => Situação.Desconhecida,
    };

    /// <summary>A data vem em pedaços, e cada um pode ser null: é o mesmo "não se sabe" do MAL.</summary>
    private static DataParcial Data(JsonElement? data) =>
        data is { } d ? new DataParcial(d.Contagem("year"), d.Contagem("month"), d.Contagem("day")) : DataParcial.Vazia;

    /// <summary>
    /// "popularity" no AniList é quanta gente tem a obra na lista, não a posição dela. A
    /// posição está em "rankings", e só a de todos os tempos quer dizer o mesmo que no MAL.
    /// </summary>
    private static int? Ranking(JsonElement media, string tipo)
    {
        foreach (var ranking in media.Lista("rankings"))
        {
            if (ranking.Campo("allTime") is { ValueKind: JsonValueKind.True }
                && string.Equals(ranking.Texto("type"), tipo, StringComparison.Ordinal))
            {
                return ranking.Contagem("rank");
            }
        }

        return null;
    }

    /// <summary>
    /// Mangá não tem estúdio: quem assina é quem escreveu e desenhou. O staff do AniList
    /// traz a equipe inteira, inclusive assistente e designer, e só o papel separa.
    /// </summary>
    private static List<string> Autores(JsonElement media)
    {
        var autores = new List<string>();

        foreach (var pessoa in media.Campo("staff")?.Lista("edges") ?? [])
        {
            if (pessoa.Texto("role") is not { } papel || !Assina(papel))
                continue;

            if (pessoa.Campo("node")?.Campo("name")?.Texto("full") is { Length: > 0 } nome && !autores.Contains(nome))
                autores.Add(nome);
        }

        return autores;
    }

    /// <summary>
    /// Os papéis que são autoria, e nada além. Procurar "art" solto pegaria o "Touch-Up Art
    /// & Lettering" de edição estrangeira, que não escreveu nem desenhou nada.
    /// </summary>
    private static readonly string[] PapéisDeAutoria =
    [
        "story & art",
        "story and art",
        "story",
        "art",
        "original story",
        "original creator",
        "illustration",
        "story & illustration",
    ];

    private static bool Assina(string papel)
    {
        // o AniList pendura o trecho da obra no papel: "Story & Art (vols 1-41)"
        var parêntese = papel.IndexOf('(');
        var limpo = (parêntese >= 0 ? papel[..parêntese] : papel).Trim();

        return PapéisDeAutoria.Contains(limpo, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A sinopse sem o HTML que o AniList guarda junto, e sem o crédito no fim.</summary>
    internal static string? SemHtml(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpo = Quebra.Replace(texto, "\n");

        limpo = Etiqueta.Replace(limpo, string.Empty);
        limpo = WebUtility.HtmlDecode(limpo);
        limpo = Crédito.Replace(limpo, string.Empty);
        limpo = ParágrafoDemais.Replace(limpo, "\n\n").Trim();

        return limpo.Length > 0 ? limpo : null;
    }

    public void Dispose() => _http.Dispose();
}

using System.Text.Json;

namespace AlephBot.Threnodian.MyAnimeList;

/// <summary>
/// O MangaDex (api.mangadex.org): catálogo aberto de mangá, sem chave. Entra na fila do /ma
/// por dois motivos — é reserva quando o AniList não responde, e tem sinopse em português
/// escrita por gente, que é melhor do que qualquer tradução de máquina minha.
/// </summary>
public sealed class MangaDex : IMyAnimeList, ISinopseEmPortuguês, IDisposable
{
    private const string Endereço = "https://api.mangadex.org/";

    private const int Limite = 5;

    /// <summary>De onde as capas são servidas; o arquivo vem no relacionamento da obra.</summary>
    private const string Capas = "https://uploads.mangadex.org/covers";

    /// <summary>
    /// As classificações que entram. Vão explícitas, e não omitidas, pra busca não mudar
    /// de comportamento no dia em que eles mexerem no padrão da API.
    /// </summary>
    private const string Classificações =
        "contentRating[]=safe&contentRating[]=suggestive&contentRating[]=erotica";

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Endereço),
        Timeout = TimeSpan.FromSeconds(15),
    };

    public MangaDex()
    {
        // eles pedem que quem usa a API se identifique, e é justo
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AlephBot/1.0 (+https://github.com/lucaass18/AlephBot)");
    }

    public string Nome => "MangaDex";

    public async Task<IReadOnlyList<Obra>> ProcurarAsync(
        TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
    {
        // o MangaDex não tem anime: perguntar seria gastar requisição pra ouvir "não tenho"
        if (tipo == TipoDeObra.Anime)
            return [];

        using var json = await PedeAsync(
            $"manga?title={Uri.EscapeDataString(busca)}&limit={Limite}&{Classificações}"
            + "&includes[]=author&includes[]=artist&includes[]=cover_art",
            cancellationToken);

        return json.RootElement
            .Lista("data")
            .Select(Ler)
            .OfType<Obra>()
            .ToList();
    }

    /// <summary>
    /// A sinopse em português da obra, quando o MangaDex tem a mesma obra e ela tem
    /// descrição em pt-br. Null quando não dá pra ter certeza de que é a mesma: a busca
    /// deles ordena mal, e mostrar a sinopse da obra errada é pior que traduzir por máquina.
    /// </summary>
    public async Task<string?> ParaAsync(Obra obra, CancellationToken cancellationToken = default)
    {
        if (obra.Tipo != TipoDeObra.Manga)
            return null;

        try
        {
            using var json = await PedeAsync(
                $"manga?title={Uri.EscapeDataString(obra.Título)}&limit={Limite}&{Classificações}",
                cancellationToken);

            foreach (var item in json.RootElement.Lista("data"))
            {
                if (!ÉAMesmaObra(item, obra))
                    continue;

                // só vale o que está mesmo em português: o inglês dele não é melhor que o
                // da fonte que já achou a obra
                var (texto, éPortuguês) = Descrição(item.Campo("attributes")?.Campo("description"));

                return éPortuguês ? texto : null;
            }
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException or JsonException)
        {
            // sinopse em português é bônus; falhar aqui não pode estragar a ficha
        }

        return null;
    }

    /// <summary>
    /// Mesma obra só quando um id cruzado confirma. Comparar título seria confiar na ordem
    /// deles, que devolve a versão colorida e a sequência na frente do original.
    /// </summary>
    private static bool ÉAMesmaObra(JsonElement item, Obra obra)
    {
        var links = item.Campo("attributes")?.Campo("links");

        if (links is null)
            return false;

        var noMal = Número(links.Value.Texto("mal"));
        var noAniList = Número(links.Value.Texto("al"));

        return obra.Fonte switch
        {
            "AniList" => noAniList == obra.Id || noMal == obra.Id,
            "MyAnimeList" => noMal == obra.Id,
            _ => false,
        };
    }

    private async Task<JsonDocument> PedeAsync(string caminho, CancellationToken cancellationToken)
    {
        using var resposta = await _http.GetAsync(caminho, cancellationToken);

        resposta.EnsureSuccessStatusCode();

        await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);
    }

    /// <summary>Um item do "data" virando a mesma Obra das outras fontes; null se não der pra mostrar.</summary>
    internal static Obra? Ler(JsonElement item)
    {
        var atributos = item.Campo("attributes");

        if (atributos is not { } dados || Título(dados.Campo("title")) is not { } título)
            return null;

        var links = dados.Campo("links");
        var noMal = Número(links?.Texto("mal"));
        var id = item.Texto("id");

        return new Obra
        {
            Fonte = "MangaDex",
            Tipo = TipoDeObra.Manga,

            // sem id no MAL o link vai pro MangaDex, que é onde a obra existe
            Id = noMal ?? 0,
            Endereço = noMal is null && id is not null ? $"https://mangadex.org/title/{id}" : null,

            Título = título,
            TítuloEmInglês = Alternativo(dados, "en"),
            TítuloEmJaponês = Alternativo(dados, "ja"),
            Sinônimos = Sinônimos(dados, título),
            Imagem = Capa(item, id),
            Formato = Formato(dados.Texto("originalLanguage")),
            Situação = LerSituação(dados.Texto("status")),
            Capítulos = Número(dados.Texto("lastChapter")),
            Volumes = Número(dados.Texto("lastVolume")),
            Início = new DataParcial(dados.Contagem("year"), null, null),
            Sinopse = Descrição(dados.Campo("description")).Texto,
            SinopseEmPortuguês = Descrição(dados.Campo("description")).ÉPortuguês,
            Gêneros = Gêneros(dados),
            Autoria = Autoria(item),
        };
    }

    /// <summary>O título é um dicionário por idioma: inglês se houver, senão o romaji, senão o que vier.</summary>
    private static string? Título(JsonElement? títulos)
    {
        if (títulos is not { ValueKind: JsonValueKind.Object } dicionário)
            return null;

        return dicionário.Texto("en")
            ?? dicionário.Texto("ja-ro")
            ?? dicionário.EnumerateObject()
                .Select(campo => campo.Value.ValueKind == JsonValueKind.String ? campo.Value.GetString() : null)
                .FirstOrDefault(nome => !string.IsNullOrWhiteSpace(nome));
    }

    /// <summary>Os "altTitles" são uma lista de dicionários de um item só: {"ja": "ワンピース"}.</summary>
    private static string? Alternativo(JsonElement dados, string idioma) =>
        dados.Lista("altTitles").Select(alt => alt.Texto(idioma)).FirstOrDefault(nome => nome is not null);

    private static List<string> Sinônimos(JsonElement dados, string título)
    {
        var nomes = new List<string>();

        foreach (var alt in dados.Lista("altTitles"))
        {
            if (alt.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var idioma in alt.EnumerateObject())
            {
                if (idioma.Value.ValueKind != JsonValueKind.String)
                    continue;

                var nome = idioma.Value.GetString();

                if (!string.IsNullOrWhiteSpace(nome)
                    && !string.Equals(nome, título, StringComparison.OrdinalIgnoreCase)
                    && !nomes.Contains(nome))
                {
                    nomes.Add(nome);
                }
            }
        }

        return nomes;
    }

    private static string? Capa(JsonElement item, string? id)
    {
        if (id is null)
            return null;

        foreach (var relação in item.Lista("relationships"))
        {
            if (relação.Texto("type") == "cover_art"
                && relação.Campo("attributes")?.Texto("fileName") is { } arquivo)
            {
                // o .512.jpg é a versão reduzida; a original é grande demais pra miniatura
                return $"{Capas}/{id}/{arquivo}.512.jpg";
            }
        }

        return null;
    }

    /// <summary>Autor e desenhista, sem repetir quem faz as duas coisas — que é o normal em mangá.</summary>
    private static List<string> Autoria(JsonElement item)
    {
        var nomes = new List<string>();

        foreach (var relação in item.Lista("relationships"))
        {
            if (relação.Texto("type") is not ("author" or "artist"))
                continue;

            if (relação.Campo("attributes")?.Texto("name") is { Length: > 0 } nome && !nomes.Contains(nome))
                nomes.Add(nome);
        }

        return nomes;
    }

    /// <summary>
    /// Gênero e tema viram etiqueta no embed, como nas outras fontes; "format" fica de fora
    /// porque "Award Winning" não é gênero. A demografia entra junto, que é como o MAL mostra.
    /// </summary>
    private static List<string> Gêneros(JsonElement dados)
    {
        var etiquetas = new List<string>();

        foreach (var tag in dados.Lista("tags"))
        {
            var atributos = tag.Campo("attributes");

            if (atributos?.Texto("group") is not ("genre" or "theme"))
                continue;

            if (atributos?.Campo("name")?.Texto("en") is { Length: > 0 } nome && !etiquetas.Contains(nome))
                etiquetas.Add(nome);
        }

        if (dados.Texto("publicationDemographic") is { Length: > 0 } demografia)
            etiquetas.Add(char.ToUpperInvariant(demografia[0]) + demografia[1..]);

        return etiquetas;
    }

    /// <summary>
    /// A descrição em português quando existe; senão a em inglês, que todo mundo tem. Quem
    /// chama precisa saber a diferença: português não passa pelo tradutor, inglês passa.
    /// </summary>
    private static (string? Texto, bool ÉPortuguês) Descrição(JsonElement? descrição)
    {
        if (descrição is not { ValueKind: JsonValueKind.Object } textos)
            return (null, false);

        var português = textos.Texto("pt-br") ?? textos.Texto("pt");

        if (!string.IsNullOrWhiteSpace(português))
            return (português.Trim(), true);

        var inglês = textos.Texto("en");

        return string.IsNullOrWhiteSpace(inglês) ? (null, false) : (inglês.Trim(), false);
    }

    /// <summary>Mangá é japonês; coreano é manhwa e chinês é manhua, e quem lê sabe a diferença.</summary>
    private static string Formato(string? idioma) => (idioma ?? string.Empty).Split('-')[0] switch
    {
        "ko" => "manhwa",
        "zh" => "manhua",
        _ => "manga",
    };

    private static Situação LerSituação(string? texto) => Obra.Normaliza(texto) switch
    {
        "ongoing" => Situação.EmAndamento,
        "completed" => Situação.Concluída,
        "hiatus" => Situação.EmHiato,
        "cancelled" => Situação.Cancelada,
        _ => Situação.Desconhecida,
    };

    /// <summary>
    /// Eles mandam número como texto, e vazio quando não sabem: "364" vira 364, "" vira
    /// null. Capítulo pode ter decimal ("364.5"); o que interessa é a parte inteira.
    /// </summary>
    private static int? Número(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var inteiro = texto.Split('.')[0];

        return int.TryParse(inteiro, out var valor) && valor > 0 ? valor : null;
    }

    public void Dispose() => _http.Dispose();
}

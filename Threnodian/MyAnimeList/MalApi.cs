using System.Text.Json;

namespace AlephBot.Threnodian.MyAnimeList;

/// <summary>
/// A API oficial do MyAnimeList (api.myanimelist.net/v2). Pede um Client ID, criado de
/// graça em myanimelist.net/apiconfig, e em troca não depende de ninguém raspar o site: é
/// a fonte que atende quando MAL_CLIENT_ID está no .env.
/// </summary>
public sealed class MalApi : IMyAnimeList, IDisposable
{
    private const string Endereço = "https://api.myanimelist.net/v2/";

    private const int Limite = 5;

    // a API só manda o que eu pedir por nome; sem "fields" vem só id, título e capa
    private const string CamposComuns =
        "id,title,main_picture,alternative_titles,start_date,end_date,synopsis,mean,rank,popularity,num_scoring_users,genres,media_type,status";

    private const string CamposAnime = CamposComuns + ",num_episodes,rating,studios";
    private const string CamposManga = CamposComuns + ",num_volumes,num_chapters,authors{first_name,last_name}";

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Endereço),
        Timeout = TimeSpan.FromSeconds(15),
    };

    public MalApi(string clientId)
    {
        _http.DefaultRequestHeaders.Add("X-MAL-CLIENT-ID", clientId);
    }

    public string Nome => "MyAnimeList";

    public async Task<IReadOnlyList<Obra>> ProcurarAsync(
        TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
    {
        var campos = tipo == TipoDeObra.Anime ? CamposAnime : CamposManga;

        // sem nsfw=true a API já deixa o conteúdo adulto de fora, que é o que eu quero num servidor
        var url = $"{Obra.Caminho(tipo)}?q={Uri.EscapeDataString(busca)}&limit={Limite}&fields={campos}";

        using var resposta = await _http.GetAsync(url, cancellationToken);

        resposta.EnsureSuccessStatusCode();

        await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);

        // a lista vem embrulhada: data[].node é a obra, e ao lado dela viria o status na minha lista
        return json.RootElement
            .Lista("data")
            .Select(item => item.Campo("node"))
            .OfType<JsonElement>()
            .Select(node => Ler(tipo, node))
            .OfType<Obra>()
            .ToList();
    }

    private static Obra? Ler(TipoDeObra tipo, JsonElement node)
    {
        if (node.Contagem("id") is not { } id || node.Texto("title") is not { } título)
            return null;

        var éAnime = tipo == TipoDeObra.Anime;
        var alternativos = node.Campo("alternative_titles");

        return new Obra
        {
            Tipo = tipo,
            Id = id,
            Título = título,
            TítuloEmInglês = alternativos?.Texto("en"),
            TítuloEmJaponês = alternativos?.Texto("ja"),
            Sinônimos = alternativos?.Textos("synonyms") ?? [],
            Imagem = node.Campo("main_picture") is { } capa
                ? capa.Texto("large") ?? capa.Texto("medium")
                : null,
            Formato = Obra.Normaliza(node.Texto("media_type")),
            Situação = Obra.LerSituação(node.Texto("status")),
            Episódios = éAnime ? node.Contagem("num_episodes") : null,
            Capítulos = éAnime ? null : node.Contagem("num_chapters"),
            Volumes = éAnime ? null : node.Contagem("num_volumes"),
            Início = DataParcial.De(node.Texto("start_date")),
            Fim = DataParcial.De(node.Texto("end_date")),
            Nota = node.Decimal("mean"),
            Votos = node.Contagem("num_scoring_users"),
            Posição = node.Contagem("rank"),
            Popularidade = node.Contagem("popularity"),
            Sinopse = node.Texto("synopsis"),

            // aqui gênero, tema e demografia já vêm juntos numa lista só
            Gêneros = node.Nomes("genres"),
            Autoria = éAnime
                ? node.Nomes("studios")
                : node.Lista("authors").Select(NomeDeAutor).OfType<string>().ToList(),
            Classificação = éAnime ? Obra.Normaliza(node.Texto("rating")) : null,
        };
    }

    /// <summary>Cada autor vem como { node: { first_name, last_name }, role }; junto os dois nomes.</summary>
    private static string? NomeDeAutor(JsonElement autor)
    {
        if (autor.Campo("node") is not { } pessoa)
            return null;

        var nome = $"{pessoa.Texto("first_name")} {pessoa.Texto("last_name")}".Trim();

        return nome.Length > 0 ? nome : null;
    }

    public void Dispose() => _http.Dispose();
}

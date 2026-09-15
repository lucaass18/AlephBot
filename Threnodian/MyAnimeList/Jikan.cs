using System.Text.Json;

namespace AlephBot.Threnodian.MyAnimeList;

/// <summary>
/// O Jikan (api.jikan.moe): a API não-oficial do MAL, que lê o site e devolve JSON. Não pede
/// chave, então é o que atende quando não tem MAL_CLIENT_ID no .env. O preço é depender do
/// site: quando o MAL barra o Jikan, a busca volta 504 e aqui vira exceção.
/// </summary>
public sealed class Jikan : IMyAnimeList, IDisposable
{
    private const string Endereço = "https://api.jikan.moe/v4/";

    /// <summary>Quantos resultados eu peço: o primeiro quase sempre serve, os outros são pra achar o título exato.</summary>
    private const int Limite = 5;

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Endereço),
        Timeout = TimeSpan.FromSeconds(15),
    };

    public string Nome => "MyAnimeList (via Jikan)";

    public async Task<IReadOnlyList<Obra>> ProcurarAsync(
        TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
    {
        // sfw: isto responde dentro de servidor de Discord, e hentai não entra na conversa
        var url = $"{Obra.Caminho(tipo)}?q={Uri.EscapeDataString(busca)}&limit={Limite}&sfw=true";

        using var resposta = await _http.GetAsync(url, cancellationToken);

        resposta.EnsureSuccessStatusCode();

        await using var conteúdo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(conteúdo, cancellationToken: cancellationToken);

        return json.RootElement
            .Lista("data")
            .Select(item => Ler(tipo, item))
            .OfType<Obra>()
            .ToList();
    }

    /// <summary>Um item do "data" da busca; null se vier sem id, que é como eu sei que não é uma obra.</summary>
    private static Obra? Ler(TipoDeObra tipo, JsonElement item)
    {
        if (item.Contagem("mal_id") is not { } id || item.Texto("title") is not { } título)
            return null;

        var éAnime = tipo == TipoDeObra.Anime;

        // anime é exibido ("aired"), mangá é publicado ("published"); o formato é o mesmo
        var período = item.Campo(éAnime ? "aired" : "published");

        return new Obra
        {
            Tipo = tipo,
            Id = id,
            Título = título,
            TítuloEmInglês = item.Texto("title_english"),
            TítuloEmJaponês = item.Texto("title_japanese"),
            Sinônimos = item.Textos("title_synonyms"),
            Imagem = item.Campo("images")?.Campo("jpg") is { } jpg
                ? jpg.Texto("large_image_url") ?? jpg.Texto("image_url")
                : null,
            Formato = Obra.Normaliza(item.Texto("type")),
            Situação = Obra.LerSituação(item.Texto("status")),
            Episódios = éAnime ? item.Contagem("episodes") : null,
            Capítulos = éAnime ? null : item.Contagem("chapters"),
            Volumes = éAnime ? null : item.Contagem("volumes"),
            Início = Data(período, "from"),
            Fim = Data(período, "to"),
            Nota = item.Decimal("score"),
            Votos = item.Contagem("scored_by"),
            Posição = item.Contagem("rank"),
            Popularidade = item.Contagem("popularity"),
            Sinopse = item.Texto("synopsis"),

            // o MAL separa gênero, tema e demografia; no embed é tudo etiqueta
            Gêneros =
            [
                .. item.Nomes("genres"),
                .. item.Nomes("themes"),
                .. item.Nomes("demographics"),
            ],
            Autoria = éAnime
                ? item.Nomes("studios")
                : item.Nomes("authors").Select(NomeDeAutor).ToList(),
            Classificação = éAnime ? Classificação(item.Texto("rating")) : null,
        };
    }

    /// <summary>
    /// O "prop" traz dia, mês e ano separados e nulos quando o MAL não sabe. O "from" em
    /// ISO preenche o que falta com 1º de janeiro, e eu não quero inventar estreia.
    /// </summary>
    private static DataParcial Data(JsonElement? período, string lado)
    {
        if (período?.Campo("prop")?.Campo(lado) is { } prop)
            return new DataParcial(prop.Contagem("year"), prop.Contagem("month"), prop.Contagem("day"));

        return DataParcial.De(período?.Texto(lado));
    }

    /// <summary>"PG-13 - Teens 13 or older" vira "pg 13": só o código, do jeito que a Denia conhece.</summary>
    private static string? Classificação(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var fim = texto.IndexOf(" - ", StringComparison.Ordinal);

        return Obra.Normaliza(fim > 0 ? texto[..fim] : texto);
    }

    /// <summary>O Jikan escreve "Miura, Kentarou"; ninguém chama o autor assim.</summary>
    private static string NomeDeAutor(string nome)
    {
        var vírgula = nome.IndexOf(',');

        if (vírgula < 0)
            return nome.Trim();

        var sobrenome = nome[..vírgula].Trim();
        var primeiro = nome[(vírgula + 1)..].Trim();

        return $"{primeiro} {sobrenome}".Trim();
    }

    public void Dispose() => _http.Dispose();
}

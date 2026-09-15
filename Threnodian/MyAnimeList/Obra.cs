namespace AlephBot.Threnodian.MyAnimeList;

public enum TipoDeObra
{
    Anime,
    Manga,
}

/// <summary>
/// Onde a obra está na vida. As duas fontes dizem isso com palavras diferentes ("Currently
/// Airing" numa, "currently_airing" na outra); aqui vira uma coisa só, e quem traduz pra
/// português é a Denia.
/// </summary>
public enum Situação
{
    Desconhecida,
    EmAndamento,
    Concluída,
    NãoLançada,
    EmHiato,
    Cancelada,
}

/// <summary>
/// Data como o MAL guarda: às vezes só o ano, às vezes ano e mês. Preencher o que falta
/// com 1º de janeiro seria inventar estreia.
/// </summary>
public readonly record struct DataParcial(int? Ano, int? Mês, int? Dia)
{
    public static readonly DataParcial Vazia = new(null, null, null);

    public bool ÉVazia => Ano is null;

    /// <summary>Lê "2002-10-03", "2002-10", "2002" e também o ISO completo com hora.</summary>
    public static DataParcial De(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return Vazia;

        // "2002-10-03T00:00:00+00:00": só a data interessa
        var partes = texto.Split('T')[0].Split('-');

        return new DataParcial(
            Parte(partes, 0),
            Parte(partes, 1),
            Parte(partes, 2));
    }

    private static int? Parte(string[] partes, int índice) =>
        índice < partes.Length && int.TryParse(partes[índice], out var valor) && valor > 0
            ? valor
            : null;

    /// <summary>"03/10/2002", "10/2002" ou "2002" — o que se sabe, e nada além.</summary>
    public override string ToString() => (Ano, Mês, Dia) switch
    {
        (null, _, _) => "?",
        ({ } ano, null, _) => $"{ano}",
        ({ } ano, { } mês, null) => $"{mês:D2}/{ano}",
        ({ } ano, { } mês, { } dia) => $"{dia:D2}/{mês:D2}/{ano}",
    };
}

/// <summary>
/// Um anime ou mangá do jeito que o /ma precisa: o mesmo formato venha do Jikan ou da API
/// oficial, pra quem monta o embed não saber (nem precisar saber) de onde veio.
/// </summary>
public sealed record Obra
{
    public required TipoDeObra Tipo { get; init; }

    public required int Id { get; init; }

    /// <summary>O título principal do MAL, quase sempre o romaji.</summary>
    public required string Título { get; init; }

    public string? TítuloEmInglês { get; init; }

    public string? TítuloEmJaponês { get; init; }

    /// <summary>Os outros nomes pelos quais a obra é conhecida; servem pra achar a busca exata.</summary>
    public IReadOnlyList<string> Sinônimos { get; init; } = [];

    public string? Imagem { get; init; }

    /// <summary>
    /// O formato como a fonte diz, já normalizado (minúsculo, sem "_" nem "-"): "tv",
    /// "movie", "light novel". A tradução fica na Denia.
    /// </summary>
    public string? Formato { get; init; }

    public Situação Situação { get; init; }

    public int? Episódios { get; init; }

    public int? Capítulos { get; init; }

    public int? Volumes { get; init; }

    public DataParcial Início { get; init; } = DataParcial.Vazia;

    public DataParcial Fim { get; init; } = DataParcial.Vazia;

    /// <summary>A nota média do MAL, de 0 a 10; null quando ninguém votou ainda.</summary>
    public double? Nota { get; init; }

    /// <summary>Quantas pessoas deram nota.</summary>
    public int? Votos { get; init; }

    /// <summary>Posição no ranking de nota do MAL.</summary>
    public int? Posição { get; init; }

    /// <summary>Posição no ranking de popularidade (quantas listas a obra está).</summary>
    public int? Popularidade { get; init; }

    /// <summary>A sinopse em inglês, como o MAL guarda.</summary>
    public string? Sinopse { get; init; }

    /// <summary>Gêneros, temas e demografia, em inglês, como o MAL nomeia.</summary>
    public IReadOnlyList<string> Gêneros { get; init; } = [];

    /// <summary>Estúdios (anime) ou autores (mangá).</summary>
    public IReadOnlyList<string> Autoria { get; init; } = [];

    /// <summary>
    /// A classificação indicativa, só de anime, normalizada pro código do MAL: "g", "pg",
    /// "pg 13", "r", "r+", "rx".
    /// </summary>
    public string? Classificação { get; init; }

    public string Link => $"https://myanimelist.net/{Caminho(Tipo)}/{Id}";

    public static string Caminho(TipoDeObra tipo) =>
        tipo == TipoDeObra.Anime ? "anime" : "manga";

    /// <summary>"Finished Airing", "currently_publishing", "On Hiatus"... viram a mesma enum.</summary>
    public static Situação LerSituação(string? texto)
    {
        var normal = Normaliza(texto);

        if (normal is null)
            return Situação.Desconhecida;

        // "not yet" vem antes de "airing"/"publishing" de propósito: "Not yet aired" tem os dois
        if (normal.Contains("not yet"))
            return Situação.NãoLançada;

        if (normal.Contains("finished"))
            return Situação.Concluída;

        if (normal.Contains("airing") || normal.Contains("publishing"))
            return Situação.EmAndamento;

        if (normal.Contains("hiatus"))
            return Situação.EmHiato;

        if (normal.Contains("discontinued"))
            return Situação.Cancelada;

        return Situação.Desconhecida;
    }

    /// <summary>"TV Special", "tv_special" e "one-shot" viram "tv special" e "one shot".</summary>
    public static string? Normaliza(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        return texto.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
    }
}

/// <summary>
/// Quem sabe procurar no MyAnimeList. Tem duas: a API oficial, que pede Client ID, e o
/// Jikan, que não pede nada mas depende do humor do MAL. Qual das duas atende é decidido
/// no boot, pela config.
/// </summary>
public interface IMyAnimeList
{
    /// <summary>Como a fonte se apresenta no rodapé do embed.</summary>
    string Nome { get; }

    /// <summary>
    /// Os primeiros resultados da busca, na ordem em que a fonte devolve. Problema de rede
    /// ou resposta estranha sobe como exceção: é o chamador quem sabe dizer isso do jeito
    /// certo.
    /// </summary>
    Task<IReadOnlyList<Obra>> ProcurarAsync(TipoDeObra tipo, string busca, CancellationToken cancellationToken = default);
}

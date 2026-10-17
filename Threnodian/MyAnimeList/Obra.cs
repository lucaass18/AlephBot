namespace AlephBot.Threnodian.MyAnimeList;

public enum TipoDeObra
{
    Anime,
    Manga,
}

// Onde a obra está na vida. Cada fonte diz isso com outras palavras; aqui vira uma só.
public enum Situação
{
    Desconhecida,
    EmAndamento,
    Concluída,
    NãoLançada,
    EmHiato,
    Cancelada,
}

// Data como o MAL guarda: às vezes só o ano. Completar com 1º de janeiro seria inventar estreia.
public readonly record struct DataParcial(int? Ano, int? Mês, int? Dia)
{
    public static readonly DataParcial Vazia = new(null, null, null);

    public bool ÉVazia => Ano is null;

    // Lê "2002-10-03", "2002-10", "2002" e também o ISO completo com hora.
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

    // "03/10/2002", "10/2002" ou "2002" — o que se sabe, e nada além.
    public override string ToString() => (Ano, Mês, Dia) switch
    {
        (null, _, _) => "?",
        ({ } ano, null, _) => $"{ano}",
        ({ } ano, { } mês, null) => $"{mês:D2}/{ano}",
        ({ } ano, { } mês, { } dia) => $"{dia:D2}/{mês:D2}/{ano}",
    };
}

// Um anime ou mangá como o /ma precisa: o mesmo formato venha de onde vier.
public sealed record Obra
{
    // Quem respondeu, do jeito que o rodapé do embed apresenta: nem sempre é o MAL.
    public string Fonte { get; init; } = "MyAnimeList";

    public required TipoDeObra Tipo { get; init; }

    // O id no MyAnimeList; quando a fonte não sabe o do MAL, o id dela mesma.
    public required int Id { get; init; }

    // O título principal do MAL, quase sempre o romaji.
    public required string Título { get; init; }

    public string? TítuloEmInglês { get; init; }

    public string? TítuloEmJaponês { get; init; }

    // Os outros nomes pelos quais a obra é conhecida; servem pra achar a busca exata.
    public IReadOnlyList<string> Sinônimos { get; init; } = [];

    public string? Imagem { get; init; }

    // O formato como a fonte diz, normalizado: "tv", "movie", "light novel". A tradução é da Denia.
    public string? Formato { get; init; }

    public Situação Situação { get; init; }

    public int? Episódios { get; init; }

    public int? Capítulos { get; init; }

    public int? Volumes { get; init; }

    public DataParcial Início { get; init; } = DataParcial.Vazia;

    public DataParcial Fim { get; init; } = DataParcial.Vazia;

    // A nota média do MAL, de 0 a 10; null quando ninguém votou ainda.
    public double? Nota { get; init; }

    // Quantas pessoas deram nota.
    public int? Votos { get; init; }

    // Posição no ranking de nota do MAL.
    public int? Posição { get; init; }

    // Posição no ranking de popularidade (quantas listas a obra está).
    public int? Popularidade { get; init; }

    // A sinopse como a fonte guarda — quase sempre em inglês.
    public string? Sinopse { get; init; }

    // A sinopse já está em português e não passa pelo tradutor. Só o MangaDex tem isso.
    public bool SinopseEmPortuguês { get; init; }

    // Gêneros, temas e demografia, em inglês, como o MAL nomeia.
    public IReadOnlyList<string> Gêneros { get; init; } = [];

    // Estúdios (anime) ou autores (mangá).
    public IReadOnlyList<string> Autoria { get; init; } = [];

    // O elenco na ordem da fonte (protagonistas primeiro); fonte sem elenco deixa vazio.
    public IReadOnlyList<Personagem> Personagens { get; init; } = [];

    // Quando a ficha foi buscada. Null é recém-chegada; preenchida veio do banco, e o embed avisa.
    public DateTimeOffset? VistoEm { get; init; }

    // A classificação indicativa, só de anime, no código do MAL: "g", "pg", "r", "rx".
    public string? Classificação { get; init; }

    // O endereço na fonte que respondeu, quando a obra não está no MAL.
    public string? Endereço { get; init; }

    public string Link => Endereço ?? $"https://myanimelist.net/{Caminho(Tipo)}/{Id}";

    public static string Caminho(TipoDeObra tipo) =>
        tipo == TipoDeObra.Anime ? "anime" : "manga";

    // "Finished Airing", "currently_publishing", "On Hiatus"... viram a mesma enum.
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

    // "TV Special", "tv_special" e "one-shot" viram "tv special" e "one shot".
    public static string? Normaliza(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        return texto.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
    }
}

// Alguém da obra. Nome principal é o romanizado; o que o AniList marca como spoiler não entra.
public sealed record Personagem
{
    public required string Nome { get; init; }

    public string? NomeNativo { get; init; }

    public IReadOnlyList<string> OutrosNomes { get; init; } = [];

    // Protagonista, e não parte do elenco de apoio ou de fundo.
    public bool Principal { get; init; }

    public string? Imagem { get; init; }
}

// A fonte recusou a credencial: erro de quem configurou, não queda — daí o tipo próprio.
public sealed class CredencialRecusadaException(string mensagem, Exception? causa = null)
    : Exception(mensagem, causa);

// Quem traz a sinopse em português de obra já achada. Hoje é o MangaDex.
public interface ISinopseEmPortuguês
{
    // A sinopse em português, ou null quando não há como garantir que é a mesma obra.
    Task<string?> ParaAsync(Obra obra, CancellationToken cancellationToken = default);
}

// Onde as fichas já mostradas ficam. Sem banco, Disponível é falso e ninguém guarda nada.
public interface IFichasGuardadas
{
    // Falso quando não há Postgres: aí o /ma trabalha como trabalhava antes do cache.
    bool Disponível { get; }

    // A ficha guardada pra esta busca, ou null. validade é a idade máxima aceita.
    Task<Obra?> AcharAsync(TipoDeObra tipo, string busca, TimeSpan? validade, CancellationToken cancellationToken = default);

    // Guarda a ficha que foi mostrada, ligada ao que a pessoa digitou.
    Task GuardarAsync(string busca, Obra obra, CancellationToken cancellationToken = default);

    // A fonte disse agora há pouco que não conhece isto? Vale sem banco: a memória é do processo.
    bool JáDeuEmNada(TipoDeObra tipo, string busca);

    // Anota que a fonte não conhece isto. Vale por pouco: obra nova entra no catálogo a qualquer hora.
    void DeuEmNada(TipoDeObra tipo, string busca);
}

// Quem sabe procurar anime e mangá. Quem atende é a FontesEmCadeia, montada no boot.
public interface IMyAnimeList
{
    // Como a fonte se apresenta no rodapé do embed.
    string Nome { get; }

    // Os primeiros resultados, na ordem da fonte. Erro sobe como exceção, pro chamador falar.
    Task<IReadOnlyList<Obra>> ProcurarAsync(TipoDeObra tipo, string busca, CancellationToken cancellationToken = default);
}

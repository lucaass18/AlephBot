using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// Uma ficha do /ma que já foi mostrada uma vez. Fica guardada por dois motivos: não
/// perguntar de novo à fonte o que já se sabe, e continuar respondendo quando ela cair —
/// que foi o que aconteceu quando o Jikan morreu e levou o comando junto.
/// </summary>
public sealed class ObraGuardada
{
    /// <summary>Quantos personagens cada ficha guarda. O embed mostra uma linha, não um elenco inteiro.</summary>
    public const int PersonagensNoMaximo = 10;

    public long Id { get; set; }

    /// <summary>Quem respondeu: "MyAnimeList" ou "AniList". Faz parte da identidade da ficha.</summary>
    public string Fonte { get; set; } = "";

    public TipoDeObra Tipo { get; set; }

    /// <summary>O id na fonte que respondeu; com a fonte, é o que identifica a obra.</summary>
    public int IdNaFonte { get; set; }

    public string? Endereco { get; set; }

    public string Titulo { get; set; } = "";

    public string? TituloEmIngles { get; set; }

    public string? TituloEmJapones { get; set; }

    public List<string> Sinonimos { get; set; } = [];

    public string? Imagem { get; set; }

    public string? Formato { get; set; }

    public Situação Situacao { get; set; }

    public int? Episodios { get; set; }

    public int? Capitulos { get; set; }

    public int? Volumes { get; set; }

    /// <summary>A data como o MAL guarda ("2002", "2002-10", "2002-10-03"); parcial é parcial.</summary>
    public string? Inicio { get; set; }

    public string? Fim { get; set; }

    public double? Nota { get; set; }

    public int? Votos { get; set; }

    public int? Posicao { get; set; }

    public int? Popularidade { get; set; }

    public string? Sinopse { get; set; }

    public List<string> Generos { get; set; } = [];

    public List<string> Autoria { get; set; } = [];

    public string? Classificacao { get; set; }

    /// <summary>
    /// Todos os nomes da obra numa linha só, sem acento e em minúscula. É por aqui que a
    /// ficha é achada quando a fonte está fora e ninguém buscou exatamente aquele termo.
    /// </summary>
    public string Procura { get; set; } = "";

    /// <summary>Quando esta ficha veio da fonte. É a idade que o rodapé do embed mostra.</summary>
    public DateTimeOffset VistoEm { get; set; }

    public List<PersonagemGuardado> Personagens { get; set; } = [];
}

/// <summary>
/// Alguém do elenco. Vem no mesmo pedido da ficha, então não custa requisição nenhuma — e
/// fica guardado inteiro, com os outros nomes, pro dia que existir um /personagem.
/// </summary>
public sealed class PersonagemGuardado
{
    public long Id { get; set; }

    public long ObraId { get; set; }

    /// <summary>A ordem em que a fonte devolveu: protagonista primeiro.</summary>
    public int Ordem { get; set; }

    /// <summary>O nome romanizado, que é como o personagem se escreve em inglês.</summary>
    public string Nome { get; set; } = "";

    public string? NomeNativo { get; set; }

    /// <summary>Localização oficial e apelido. O que a fonte marca como spoiler nunca chega aqui.</summary>
    public List<string> OutrosNomes { get; set; } = [];

    public bool Principal { get; set; }

    public string? Imagem { get; set; }
}

/// <summary>
/// O que alguém digitou, ligado à ficha que viu. É isto que faz a mesma busca não sair pra
/// internet duas vezes — e o que garante que amanhã ela devolve a mesma obra de hoje.
/// </summary>
public sealed class BuscaGuardada
{
    public const int TermoNoMaximo = 200;

    public long Id { get; set; }

    /// <summary>O termo normalizado: sem acento, em minúscula, sem espaço sobrando.</summary>
    public string Termo { get; set; } = "";

    public TipoDeObra Tipo { get; set; }

    public long ObraId { get; set; }

    public ObraGuardada? Obra { get; set; }

    public DateTimeOffset VistoEm { get; set; }
}

using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Threnodian.Dados;

// Uma ficha do /ma já mostrada: não perguntar duas vezes, e seguir respondendo se a fonte cair.
public sealed class ObraGuardada
{
    // Quantos personagens cada ficha guarda. O embed mostra uma linha, não um elenco inteiro.
    public const int PersonagensNoMaximo = 10;

    public long Id { get; set; }

    // Quem respondeu: "MyAnimeList" ou "AniList". Faz parte da identidade da ficha.
    public string Fonte { get; set; } = "";

    public TipoDeObra Tipo { get; set; }

    // O id na fonte que respondeu; com a fonte, é o que identifica a obra.
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

    // A data como o MAL guarda ("2002", "2002-10", "2002-10-03"); parcial é parcial.
    public string? Inicio { get; set; }

    public string? Fim { get; set; }

    public double? Nota { get; set; }

    public int? Votos { get; set; }

    public int? Posicao { get; set; }

    public int? Popularidade { get; set; }

    public string? Sinopse { get; set; }

    // A sinopse já está em português: tradutor de máquina em texto bom vira lixo.
    public bool SinopseEmPortugues { get; set; }

    public List<string> Generos { get; set; } = [];

    public List<string> Autoria { get; set; } = [];

    public string? Classificacao { get; set; }

    // Todos os nomes numa linha, sem acento e minúsculos: é por aqui que a ficha é achada.
    public string Procura { get; set; } = "";

    // Quando esta ficha veio da fonte. É a idade que o rodapé do embed mostra.
    public DateTimeOffset VistoEm { get; set; }

    public List<PersonagemGuardado> Personagens { get; set; } = [];
}

// Alguém do elenco. Vem no mesmo pedido da ficha, então não custa requisição nenhuma.
public sealed class PersonagemGuardado
{
    public long Id { get; set; }

    public long ObraId { get; set; }

    // A ordem em que a fonte devolveu: protagonista primeiro.
    public int Ordem { get; set; }

    // O nome romanizado, que é como o personagem se escreve em inglês.
    public string Nome { get; set; } = "";

    public string? NomeNativo { get; set; }

    // Localização oficial e apelido. O que a fonte marca como spoiler nunca chega aqui.
    public List<string> OutrosNomes { get; set; } = [];

    public bool Principal { get; set; }

    public string? Imagem { get; set; }
}

// O que alguém digitou, ligado à ficha que viu: a mesma busca não sai duas vezes.
public sealed class BuscaGuardada
{
    public const int TermoNoMaximo = 200;

    public long Id { get; set; }

    // O termo normalizado: sem acento, em minúscula, sem espaço sobrando.
    public string Termo { get; set; } = "";

    public TipoDeObra Tipo { get; set; }

    public long ObraId { get; set; }

    public ObraGuardada? Obra { get; set; }

    public DateTimeOffset VistoEm { get; set; }
}

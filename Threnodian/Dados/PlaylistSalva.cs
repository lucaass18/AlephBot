namespace AlephBot.Threnodian.Dados;

/// <summary>
/// Uma fila que alguém guardou com nome pra tocar outro dia. É do servidor, não de quem
/// salvou: quem monta a fila num servidor costuma ser mais de uma pessoa, e a playlist que
/// sumisse quando um saísse não serviria pra nada.
/// </summary>
public sealed class PlaylistSalva
{
    /// <summary>Quantas faixas cabem numa. Acima disso a fila demora demais pra montar.</summary>
    public const int FaixasNoMáximo = 500;

    /// <summary>Quantas playlists cada servidor pode ter.</summary>
    public const int PorServidorNoMáximo = 50;

    public const int NomeNoMáximo = 50;

    public long Id { get; set; }

    public ulong GuildId { get; set; }

    public string Nome { get; set; } = "";

    /// <summary>Quem salvou, só pra mostrar na lista — não dá direito nenhum a mais.</summary>
    public ulong CriadaPor { get; set; }

    public DateTimeOffset CriadaEm { get; set; }

    public List<FaixaDaPlaylist> Faixas { get; set; } = [];
}

/// <summary>
/// Uma faixa guardada: os mesmos dados codificados do Lavalink que a foto do player usa
/// pra retomar depois de um restart. O título fica do lado pra listar sem ter que
/// decodificar faixa por faixa.
/// </summary>
public sealed class FaixaDaPlaylist
{
    public long Id { get; set; }

    public long PlaylistId { get; set; }

    /// <summary>A ordem em que ela entra na fila; é o que faz a playlist tocar como foi salva.</summary>
    public int Ordem { get; set; }

    public string Dados { get; set; } = "";

    public string Titulo { get; set; } = "";
}

namespace AlephBot.Threnodian.Dados;

// Uma fila guardada com nome. É do servidor, não de quem salvou — senão sumiria com ele.
public sealed class PlaylistSalva
{
    // Quantas faixas cabem numa. Acima disso a fila demora demais pra montar.
    public const int FaixasNoMáximo = 500;

    // Quantas playlists cada servidor pode ter.
    public const int PorServidorNoMáximo = 50;

    public const int NomeNoMáximo = 50;

    public long Id { get; set; }

    public ulong GuildId { get; set; }

    public string Nome { get; set; } = "";

    // Quem salvou, só pra mostrar na lista — não dá direito nenhum a mais.
    public ulong CriadaPor { get; set; }

    public DateTimeOffset CriadaEm { get; set; }

    public List<FaixaDaPlaylist> Faixas { get; set; } = [];
}

// Uma faixa guardada, nos mesmos dados do Lavalink; o título fica do lado pra listar.
public sealed class FaixaDaPlaylist
{
    public long Id { get; set; }

    public long PlaylistId { get; set; }

    // A ordem em que ela entra na fila; é o que faz a playlist tocar como foi salva.
    public int Ordem { get; set; }

    public string Dados { get; set; } = "";

    public string Titulo { get; set; } = "";
}

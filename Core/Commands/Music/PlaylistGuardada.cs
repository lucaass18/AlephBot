using AlephBot.Core.Personality;
using AlephBot.Threnodian.Dados;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

// A ponte entre a fila e a playlist guardada, pelos dados codificados que a foto do player usa.
internal static class PlaylistGuardada
{
    // Quantas faixas a lista mostra antes de dizer "e mais tantas".
    private const int NaListaNoMáximo = 10;

    private const int TítuloNoMáximo = 200;

    // Nome sem espaço nas pontas e do tamanho do banco; vazio ninguém pediria de volta.
    internal static bool NomeAceitável(string? nome) =>
        nome?.Trim() is { Length: > 0 } limpo && limpo.Length <= PlaylistSalva.NomeNoMáximo;

    // A fila de agora virada faixa guardada: a que toca primeiro, e o resto na ordem.
    internal static IReadOnlyList<FaixaDaPlaylist> DaFila(QueuedLavalinkPlayer player)
    {
        var faixas = new List<FaixaDaPlaylist>();

        if (player.CurrentTrack is { } tocando)
            faixas.Add(Converte(tocando, faixas.Count));

        foreach (var item in player.Queue)
        {
            if (faixas.Count >= PlaylistSalva.FaixasNoMáximo)
                break;

            if (item.Track is { } faixa)
                faixas.Add(Converte(faixa, faixas.Count));
        }

        return faixas;
    }

    private static FaixaDaPlaylist Converte(LavalinkTrack faixa, int ordem) => new()
    {
        Ordem = ordem,
        Dados = faixa.ToString(),
        Titulo = Corta(faixa.Title),
    };

    private static string Corta(string? texto) =>
        texto is null ? "" : texto.Length <= TítuloNoMáximo ? texto : texto[..TítuloNoMáximo];

    // Põe a playlist na fila e diz quantas entraram; a que não decodifica é pulada calada.
    internal static async Task<int> ParaAFilaAsync(
        QueuedLavalinkPlayer player,
        PlaylistSalva playlist,
        string? quemPediu)
    {
        var itens = new List<ITrackQueueItem>(playlist.Faixas.Count);

        foreach (var guardada in playlist.Faixas.OrderBy(f => f.Ordem))
        {
            if (LavalinkTrack.TryParse(guardada.Dados, null, out var faixa))
                itens.Add(new FaixaPedida(new TrackReference(faixa), quemPediu));
        }

        if (itens.Count == 0)
            return 0;

        await player.Queue.AddRangeAsync(itens);

        return itens.Count;
    }

    // Com o player parado, a primeira só começa se alguém mandar; quem chama roda após responder.
    internal static ValueTask ComeçarAsync(AlephPlayer player) =>
        player.CurrentTrack is null ? player.SkipAsync() : ValueTask.CompletedTask;

    // ---- o que a pessoa vê ----------------------------------------------------

    internal static string? Recusa(PlaylistsSalvas.Resultado resultado, string nome) => resultado switch
    {
        PlaylistsSalvas.Resultado.Guardou => null,
        PlaylistsSalvas.Resultado.NomeRepetido => Denia.PlaylistNomeRepetido(nome.Trim()),
        PlaylistsSalvas.Resultado.ServidorCheio => Denia.PlaylistServidorCheio(PlaylistSalva.PorServidorNoMáximo),
        _ => Denia.PlaylistSemBanco(),
    };

    internal static EmbedProperties Guardou(string nome, int faixas) => new()
    {
        Title = "Guardei",
        Description = $"**{nome}** — {Faixas(faixas)}.\nPra tocar depois: `/playlist tocar {nome}`",
        Color = new Color(0x57F287),
        Timestamp = DateTimeOffset.UtcNow,
    };

    internal static EmbedProperties Enfileirou(PlaylistSalva playlist, int quantas)
    {
        var perdidas = playlist.Faixas.Count - quantas;

        return new EmbedProperties
        {
            Title = "Na fila",
            Description = $"**{playlist.Nome}** — {Faixas(quantas)}."
                + (perdidas > 0 ? $"\n-# {Faixas(perdidas)} não tocam mais e ficaram de fora." : ""),
            Color = new Color(0x5865F2),
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    internal static EmbedProperties Lista(IReadOnlyList<PlaylistSalva> playlists)
    {
        if (playlists.Count == 0)
        {
            return new EmbedProperties
            {
                Title = "Nenhuma playlist guardada",
                Description = "Monte uma fila e salve com `/playlist salvar <nome>`.",
                Color = new Color(0x99AAB5),
            };
        }

        var mostradas = playlists.Take(NaListaNoMáximo)
            .Select(p => $"**{p.Nome}** — {Faixas(p.Faixas.Count)}, de <@{p.CriadaPor}>");

        var sobrando = playlists.Count - NaListaNoMáximo;

        return new EmbedProperties
        {
            Title = $"Playlists guardadas ({playlists.Count})",
            Description = string.Join('\n', mostradas)
                + (sobrando > 0 ? $"\n-# e mais {sobrando}." : ""),
            Color = new Color(0x5865F2),
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static string Faixas(int quantas) => quantas == 1 ? "1 faixa" : $"{quantas} faixas";
}

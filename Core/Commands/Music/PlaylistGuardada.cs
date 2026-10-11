using AlephBot.Core.Personality;
using AlephBot.Threnodian.Dados;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// A ponte entre a fila que está tocando e a playlist que fica guardada. As faixas vão e
/// voltam pelos dados codificados do Lavalink — os mesmos que a foto do player usa pra
/// retomar tudo depois de um restart, então quem já voltava de um reboot volta daqui.
/// </summary>
internal static class PlaylistGuardada
{
    /// <summary>Quantas faixas a lista mostra antes de dizer "e mais tantas".</summary>
    private const int NaListaNoMáximo = 10;

    private const int TítuloNoMáximo = 200;

    /// <summary>
    /// Nome sem espaço nas pontas, do tamanho que o banco aceita. Vazio não serve: a pessoa
    /// não teria como pedir ela de volta depois.
    /// </summary>
    internal static bool NomeAceitável(string? nome) =>
        nome?.Trim() is { Length: > 0 } limpo && limpo.Length <= PlaylistSalva.NomeNoMáximo;

    /// <summary>
    /// A fila de agora virada faixa guardada: a que toca entra primeiro, e as da fila
    /// seguem na ordem. É assim que a playlist toca igualzinho quando voltar.
    /// </summary>
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

    /// <summary>
    /// Põe a playlist na fila e devolve quantas entraram. Faixa que não decodifica é
    /// pulada em silêncio: uma que o Lavalink não entende mais não pode levar junto as
    /// outras quarenta que ainda tocam.
    /// </summary>
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

        // com o player parado, a primeira só começa se alguém mandar
        if (player.CurrentTrack is null)
            await player.SkipAsync();

        return itens.Count;
    }

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

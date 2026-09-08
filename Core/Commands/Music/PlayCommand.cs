using AlephBot.Core.Personality;

using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// /play — entra no canal de quem chamou, procura a faixa e toca (ou põe na fila).
/// É o único comando de música que me arrasta pra dentro de um canal de voz.
/// </summary>
public sealed class PlayCommand : MusicSlashModule
{
    public PlayCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "play";
    public override string Description => "Toca uma música ou põe ela na fila.";
    public override string? Usage => "play <nome ou link>";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("play", "Toca uma música ou põe ela na fila.")]
    public async Task PlayAsync(
        [SlashCommandParameter(Name = "busca", Description = "Nome da música, link do YouTube/SoundCloud, ou uma playlist.")] string busca)
    {
        // entrar no canal e buscar no Lavalink passa fácil dos 3 segundos que o Discord
        // dá pra responder uma interação, então aqui a resposta vem por followup
        await DeferAsync();

        var (player, erro) = await Music.PlayerAsync(Áudio, Context, Context.Channel, conectar: true);

        if (erro is not null)
        {
            await EditarComErroAsync(erro);
            return;
        }

        var resposta = await Play.ExecutarAsync(Áudio, player!, busca, Context.User.Username);

        if (resposta.Erro is { } falha)
            await EditarComErroAsync(falha);
        else
            await EditarRespostaAsync(resposta.Embed!);
    }
}

/// <summary>!play — mesma busca do /play, para quem prefere o prefixo.</summary>
public sealed class PlayTextCommand : MusicTextModule
{
    public PlayTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "play";
    public override string Description => "Toca uma música ou põe ela na fila.";
    public override string? Usage => "play <nome ou link>";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("play", "p", "tocar")]
    public Task PlayAsync([CommandParameter(Remainder = true)] string busca) =>
        ExecutarAsync(
            player => Play.ExecutarAsync(Áudio, player, busca, Context.User.Username),
            conectar: true);
}

internal static class Play
{
    /// <summary>Procura, enfileira e diz o que aconteceu. O /play e o !play caem os dois aqui.</summary>
    internal static async Task<Music.Resposta> ExecutarAsync(
        IAudioService áudio,
        AlephPlayer player,
        string busca,
        string quemPediu,
        CancellationToken cancellationToken = default)
    {
        busca = busca.Trim();

        TrackLoadResult resultado;

        try
        {
            resultado = await áudio.Tracks.LoadTracksAsync(
                busca, Music.ModoDeBusca(busca), cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (Music.ÉServidorFora(ex))
        {
            return Music.Resposta.Falha(Denia.MúsicaServidorFora());
        }

        if (resultado.IsFailed)
            return Music.Resposta.Falha(
                Denia.MúsicaBuscaFalhou(resultado.Exception?.Message ?? "o servidor não disse por quê"));

        if (!resultado.HasMatches)
            return Music.Resposta.Falha(Denia.MúsicaNãoAchei(busca));

        return resultado.IsPlaylist
            ? await PlaylistAsync(player, resultado, busca, quemPediu, cancellationToken)
            : await UmaFaixaAsync(player, resultado.Track, quemPediu, cancellationToken);
    }

    private static async Task<Music.Resposta> UmaFaixaAsync(
        AlephPlayer player, LavalinkTrack faixa, string quemPediu, CancellationToken cancellationToken)
    {
        // a marca vai antes do PlayAsync de propósito: o evento de início chega pelo
        // websocket e pode ganhar a corrida da linha seguinte — se ele chegasse primeiro
        // com a marca ainda em falso, o canal receberia o mesmo embed duas vezes
        var item = new FaixaPedida(new TrackReference(faixa), quemPediu) { JáAnunciada = true };

        var posição = await player.PlayAsync(item, enqueue: true, cancellationToken: cancellationToken);

        if (posição == 0)
            return Music.Resposta.Ok(Music.EmbedTocando(faixa, quemPediu, player.Volume));

        // entrou na fila e não começou: quem anuncia, quando chegar a vez dela, é o player
        item.JáAnunciada = false;

        return Music.Resposta.Ok(Music.EmbedNaFila(faixa, posição, quemPediu));
    }

    private static async Task<Music.Resposta> PlaylistAsync(
        AlephPlayer player, TrackLoadResult resultado, string busca, string quemPediu, CancellationToken cancellationToken)
    {
        var faixas = resultado.Tracks;

        var itens = faixas
            .Select(faixa => (ITrackQueueItem)new FaixaPedida(new TrackReference(faixa), quemPediu))
            .ToArray();

        // nenhuma faixa vem marcada como já anunciada: a resposta aqui fala da playlist,
        // e quem apresenta a primeira faixa é o anúncio do player
        if (player.CurrentTrack is null)
        {
            await player.PlayAsync(itens[0], enqueue: true, cancellationToken: cancellationToken);

            if (itens.Length > 1)
                await player.Queue.AddRangeAsync(itens[1..], cancellationToken);
        }
        else
        {
            await player.Queue.AddRangeAsync(itens, cancellationToken);
        }

        // o Lavalink devolve a playlist sem nome de vez em quando; aí o que o usuário
        // digitou identifica melhor do que um rótulo genérico
        var nome = resultado.Playlist?.Name is { Length: > 0 } título ? título : busca;

        return Music.Resposta.Ok(Music.EmbedPlaylist(nome, faixas, quemPediu));
    }
}

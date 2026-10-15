using AlephBot.Core.Personality;

using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Music;

// /play — procura e toca. É o único comando que me arrasta pra dentro de um canal de voz.
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
        // a busca não espera nem o defer: ela não depende do Discord nem do canal de voz, e em
        // série essas esperas eram todas somadas antes de a primeira nota sair
        var procura = Play.Procurar(Áudio, busca);

        // entrar no canal e buscar passa fácil dos 3 segundos que o Discord dá; peço tempo
        await DeferAsync();

        var (player, erro) = await Music.PlayerAsync(Áudio, Context, Context.Channel, conectar: true);

        if (erro is not null)
        {
            await EditarComErroAsync(erro);
            return;
        }

        var resposta = await Play.ExecutarAsync(procura, player!, busca, Context.User.Username);

        // fora do ExecutarAsync por causa do defer; a foto continua sendo minha
        player!.Fotografar();

        if (resposta.Erro is { } falha)
            await EditarComErroAsync(falha);
        else if (resposta.Cartão is { } cartão)
            await EditarRespostaAsync(cartão.Container, cartão.Anexo);
        else
            await EditarRespostaAsync(resposta.Embed!);
    }
}

// !play — mesma busca do /play, para quem prefere o prefixo.
public sealed class PlayTextCommand : MusicTextModule
{
    public PlayTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "play";
    public override string Description => "Toca uma música ou põe ela na fila.";
    public override string? Usage => "play <nome ou link>";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("play", "p", "tocar")]
    public Task PlayAsync([CommandParameter(Remainder = true)] string busca)
    {
        // igual ao /play: a busca sai agora, e a entrada no canal corre ao lado dela
        var procura = Play.Procurar(Áudio, busca);

        return ExecutarAsync(
            player => Play.ExecutarAsync(procura, player, busca, Context.User.Username),
            conectar: true);
    }
}

internal static class Play
{
    // Manda procurar já, sem esperar player nenhum: quem vai esperar é o ExecutarAsync.
    internal static Task<(TrackLoadResult Resultado, string? Erro)> Procurar(
        IAudioService áudio, string busca, CancellationToken cancellationToken = default) =>
        Music.Adiantada(Music.CarregarAsync(áudio, Music.SemRádio(busca.Trim()), cancellationToken));

    // Espera a busca, enfileira e diz o que aconteceu. O /play e o !play caem os dois aqui.
    internal static async Task<Music.Resposta> ExecutarAsync(
        Task<(TrackLoadResult Resultado, string? Erro)> procura,
        AlephPlayer player,
        string busca,
        string quemPediu,
        CancellationToken cancellationToken = default)
    {
        // o mesmo texto que foi procurado: é ele que aparece no "não achei" e na playlist sem nome
        busca = Music.SemRádio(busca.Trim());

        var (resultado, erro) = await procura;

        if (erro is not null)
            return Music.Resposta.Falha(erro);

        if (!resultado.HasMatches || resultado.Track is not { } faixa)
            return Music.Resposta.Falha(Denia.MúsicaNãoAchei(busca));

        return resultado.IsPlaylist
            ? await PlaylistAsync(player, resultado, busca, quemPediu, cancellationToken)
            : await UmaFaixaAsync(player, faixa, quemPediu, cancellationToken);
    }

    private static async Task<Music.Resposta> UmaFaixaAsync(
        AlephPlayer player, LavalinkTrack faixa, string quemPediu, CancellationToken cancellationToken)
    {
        // marco antes do PlayAsync: o evento de início pode ganhar a corrida e dobrar o embed
        var item = player.Pedido(faixa, quemPediu, jáAnunciada: true);

        // a capa baixa enquanto o link fica pronto: o card sai junto com o som, e não depois dele
        CapaDaFaixa.Adiantar(faixa, logger: null);

        var posição = await player.PlayAsync(item, enqueue: true, cancellationToken: cancellationToken);

        if (posição == 0)
            return Music.Resposta.Ok(
                await Music.CartãoTocandoAsync(
                    faixa, quemPediu, Music.Próxima(player), cancellationToken: cancellationToken));

        // entrou na fila e não começou: quem anuncia, quando chegar a vez dela, é o player
        item.JáAnunciada = false;
        player.PrepararPróxima();

        return Music.Resposta.Ok(Music.EmbedNaFila(faixa, posição, quemPediu));
    }

    // Põe a playlist inteira na fila e anuncia de uma vez; o /playlist add chega aqui também.
    internal static async Task<Music.Resposta> PlaylistAsync(
        AlephPlayer player, TrackLoadResult resultado, string busca, string quemPediu, CancellationToken cancellationToken)
    {
        var faixas = resultado.Tracks;

        var itens = faixas
            .Select(faixa => (ITrackQueueItem)player.Pedido(faixa, quemPediu))
            .ToArray();

        // não marco nenhuma: aqui eu falo da playlist, e quem apresenta a primeira é o player
        var começouAgora = player.CurrentTrack is null;

        if (começouAgora)
        {
            // quem apresenta a primeira é o player, mas a capa dela já pode ir baixando
            CapaDaFaixa.Adiantar(faixas[0], logger: null);

            await player.PlayAsync(itens[0], enqueue: true, cancellationToken: cancellationToken);

            if (itens.Length > 1)
                await player.Queue.AddRangeAsync(itens[1..], cancellationToken);
        }
        else
        {
            await player.Queue.AddRangeAsync(itens, cancellationToken);
        }

        player.PrepararPróxima();

        // playlist às vezes vem sem nome; aí o que a pessoa digitou diz mais que um rótulo vazio
        var nome = resultado.Playlist?.Name is { Length: > 0 } título ? título : busca;

        return Music.Resposta.Ok(Music.EmbedPlaylist(nome, faixas, quemPediu, começouAgora));
    }
}

using AlephBot.Core.Personality;
using AlephBot.Threnodian.Players;

using Lavalink4NET.InactivityTracking.Players;
using Lavalink4NET.InactivityTracking.Trackers;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// Uma faixa na fila com o nome de quem pediu. O <see cref="TrackQueueItem"/> da biblioteca
/// só carrega a faixa, e aí o "pedido por" some quando a mensagem do /play rola pra cima.
/// </summary>
public sealed class FaixaPedida : ITrackQueueItem
{
    public FaixaPedida(TrackReference reference, string? quemPediu)
    {
        Reference = reference;
        QuemPediu = quemPediu;
    }

    public TrackReference Reference { get; }

    public string? QuemPediu { get; }

    /// <summary>
    /// Quem enfileirou já mostrou a faixa na resposta, então eu fico quieta quando ela
    /// começar. Vale uma vez só — se ela voltar no loop, eu anuncio de novo.
    /// </summary>
    public bool JáAnunciada { get; set; }
}

/// <summary>
/// O <see cref="AlephPlayer"/> precisa saber em que canal de texto ele fala.
/// Record porque a classe base é record — o C# não deixa misturar os dois.
/// </summary>
public sealed record AlephPlayerOptions : QueuedLavalinkPlayerOptions
{
    public TextChannel? Canal { get; set; }
}

/// <summary>
/// O player da fila com três manias minhas: anuncio sozinha a faixa que começou — é o que
/// faz a fila andar sem ninguém digitar nada —, me despeço antes de sair do canal, e deixo
/// uma foto de mim guardada pra voltar ao mesmo ponto se eu reiniciar.
/// </summary>
public sealed class AlephPlayer : QueuedLavalinkPlayer, IInactivityPlayerListener
{
    /// <summary>
    /// Quantas faixas seguidas podem falhar antes de eu desistir da fila inteira.
    ///
    /// Faixa que falha não interrompe nada: a fila anda, a próxima começa, e o anúncio de
    /// início sai. Com uma playlist onde a fonte está barrada, isso vira uma mensagem por
    /// faixa em rajada — o Discord começa a limitar e o canal fica ilegível. Três seguidas
    /// já são prova suficiente de que o problema é a fonte, não a faixa.
    /// </summary>
    private const int FalhasSeguidasAtéDesistir = 3;

    private readonly TextChannel? _canal;
    private readonly PlayerSnapshotStore _fotos;
    private readonly ILogger<AlephPlayer> _logger;

    private int _falhasSeguidas;

    public AlephPlayer(IPlayerProperties<AlephPlayer, AlephPlayerOptions> properties)
        : base(properties)
    {
        _canal = properties.Options.Value.Canal;
        _fotos = properties.ServiceProvider!.GetRequiredService<PlayerSnapshotStore>();
        _logger = properties.Logger;
    }

    protected override async ValueTask NotifyTrackStartedAsync(
        ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackStartedAsync(track, cancellationToken);

        Fotografar();

        if (track is FaixaPedida { JáAnunciada: true } pedida)
        {
            pedida.JáAnunciada = false;
            return;
        }

        if (track.Track is not { } faixa)
            return;

        await FalarAsync(Music.EmbedTocando(faixa, Music.QuemPediu(track), Volume), cancellationToken);
    }

    /// <summary>
    /// Guarda a foto de agora. Eu mesma chamo quando a faixa muda; os comandos chamam
    /// depois de mexer em fila, pausa, volume ou loop — é a única forma de eu saber, porque
    /// a biblioteca não me avisa do que fazem com a fila. E o desligamento chama por último,
    /// pela posição exata: fora dele, um crash volta a faixa pro ponto da última foto.
    /// </summary>
    internal void Fotografar()
    {
        try
        {
            _fotos.Guardar(Foto());
        }
        catch (Exception erro)
        {
            // foto que falha é restart sem retomada — nunca música interrompida agora
            _logger.LogWarning(erro, "Não consegui fotografar o player da guild {Guild}", GuildId);
        }
    }

    private PlayerSnapshot Foto()
    {
        var fila = new List<FaixaGuardada>(Queue.Count);

        foreach (var item in Queue)
        {
            if (item.Track is { } faixa)
                fila.Add(Guardada(faixa, Music.QuemPediu(item)));
        }

        return new PlayerSnapshot(
            GuildId,
            VoiceChannelId,
            _canal?.Id,
            CurrentItem?.Track is { } atual ? Guardada(atual, Music.QuemPediu(CurrentItem)) : null,
            Position?.Position ?? TimeSpan.Zero,
            IsPaused,
            fila,
            Volume,
            RepeatMode,
            DateTimeOffset.UtcNow);
    }

    private static FaixaGuardada Guardada(LavalinkTrack faixa, string? quemPediu) =>
        new(faixa.ToString(), quemPediu);

    /// <summary>
    /// A faixa começou e morreu no meio do caminho — fonte bloqueada, vídeo restrito, região.
    /// Uma eu pulo calada, que é o comportamento de sempre; a sequência delas eu corto.
    /// </summary>
    protected override async ValueTask NotifyTrackExceptionAsync(
        ITrackQueueItem track, TrackException exception, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackExceptionAsync(track, exception, cancellationToken);

        _falhasSeguidas++;

        _logger.LogWarning(
            "Faixa falhou ({Falhas} seguidas): {Faixa} — {Erro}",
            _falhasSeguidas,
            track.Track?.Title ?? "sem título",
            Motivo(exception));

        if (_falhasSeguidas < FalhasSeguidasAtéDesistir)
            return;

        // a fila toda veio da mesma fonte: insistir só rende mais anúncio e mais rate limit
        await Queue.ClearAsync(cancellationToken);
        await StopAsync(cancellationToken);

        await FalarAsync(
            new EmbedProperties
            {
                Description = $"⛔ {Denia.MúsicaDesisti(_falhasSeguidas, Motivo(exception))}",
                Color = new Color(Music.Roxo),
            },
            cancellationToken);

        _falhasSeguidas = 0;
    }

    /// <summary>
    /// Qualquer fim que não seja falha zera a contagem: o que me interessa é sequência de
    /// falhas, não o total desde que o player nasceu.
    ///
    /// Zerar no início da faixa não serviria — o início da faixa que falha vem antes da
    /// falha dela, e a contagem nunca sairia de um.
    /// </summary>
    protected override async ValueTask NotifyTrackEndedAsync(
        ITrackQueueItem track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        if (endReason is not TrackEndReason.LoadFailed)
            _falhasSeguidas = 0;

        await base.NotifyTrackEndedAsync(track, endReason, cancellationToken);

        // a fila andou (ou acabou): a foto anterior ainda mostrava esta faixa como atual
        Fotografar();
    }

    /// <summary>
    /// Saí do canal — por comando, por inatividade ou porque me tiraram. Não tem o que
    /// retomar depois disso; a foto só serve pra restart no meio da música.
    /// </summary>
    protected override ValueTask DisposeAsyncCore()
    {
        _fotos.Esquecer(GuildId);
        return base.DisposeAsyncCore();
    }

    async ValueTask IInactivityPlayerListener.NotifyPlayerInactiveAsync(
        PlayerTrackingState trackingState, CancellationToken cancellationToken)
    {
        // o rastreador me destrói logo depois daqui: ou eu falo agora, ou não falo nunca
        await FalarAsync(
            new EmbedProperties
            {
                Description = $"💤 {Denia.MúsicaInativa()}",
                Color = new Color(Music.Roxo),
            },
            cancellationToken);
    }

    ValueTask IInactivityPlayerListener.NotifyPlayerActiveAsync(
        PlayerTrackingState trackingState, CancellationToken cancellationToken) => default;

    ValueTask IInactivityPlayerListener.NotifyPlayerTrackedAsync(
        PlayerTrackingState trackingState, CancellationToken cancellationToken) => default;

    /// <summary>O Lavalink pode mandar a falha sem texto nenhum; aí eu digo isso mesmo.</summary>
    private static string Motivo(TrackException exception) =>
        exception.Message is { Length: > 0 } texto ? texto : "sem motivo";

    /// <summary>
    /// Fala no canal onde o player nasceu. Canal apagado ou permissão retirada não pode
    /// derrubar a música: o erro vira log e a faixa segue tocando.
    /// </summary>
    internal async ValueTask FalarAsync(EmbedProperties embed, CancellationToken cancellationToken)
    {
        if (_canal is null)
            return;

        try
        {
            await _canal.SendMessageAsync(
                new MessageProperties { Embeds = [embed] },
                cancellationToken: cancellationToken);
        }
        catch (RestException ex)
        {
            _logger.LogWarning(
                ex, "Não consegui falar no canal {Channel} da guild {Guild}", _canal.Id, GuildId);
        }
    }
}

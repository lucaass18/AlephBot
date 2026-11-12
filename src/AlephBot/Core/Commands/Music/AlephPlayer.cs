using AlephBot.Core.Personality;
using AlephBot.Threnodian.Players;
using AlephBot.Threnodian.Youtube;

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

// Uma faixa na fila com o nome de quem pediu, que o item da biblioteca não carrega.
public sealed class FaixaPedida : ITrackQueueItem
{
    public FaixaPedida(TrackReference reference, string? quemPediu)
    {
        Reference = reference;
        QuemPediu = quemPediu;
    }

    public TrackReference Reference { get; }

    public string? QuemPediu { get; }

    // Quem enfileirou já viu a faixa, então eu fico quieta; vale uma vez, o loop anuncia de novo.
    public bool JáAnunciada { get; set; }
}

// O canal de texto onde o player fala. Record porque a classe base é record.
public sealed record AlephPlayerOptions : QueuedLavalinkPlayerOptions
{
    public TextChannel? Canal { get; set; }
}

// O player da fila com três manias: anuncio a faixa, me despeço antes de sair e me fotografo.
public sealed class AlephPlayer : QueuedLavalinkPlayer, IInactivityPlayerListener
{
    // Quantas falham seguidas antes de eu desistir: três já provam que o problema é a fonte.
    private const int FalhasSeguidasAtéDesistir = 3;

    private readonly TextChannel? _canal;
    private readonly PlayerSnapshotStore _fotos;
    private readonly YoutubeAudio? _youtube;
    private readonly ILogger<AlephPlayer> _logger;

    private int _falhasSeguidas;

    public AlephPlayer(IPlayerProperties<AlephPlayer, AlephPlayerOptions> properties)
        : base(properties)
    {
        _canal = properties.Options.Value.Canal;
        _fotos = properties.ServiceProvider!.GetRequiredService<PlayerSnapshotStore>();
        _youtube = properties.ServiceProvider!.GetService<YoutubeAudio>();
        _logger = properties.Logger;
    }

    // A faixa como item da fila. A do YouTube sai envolvida pro yt-audio — toda faixa passa aqui.
    internal FaixaPedida Pedido(LavalinkTrack faixa, string? quemPediu, bool jáAnunciada = false) =>
        new(new TrackReference(_youtube?.Envolver(faixa) ?? faixa), quemPediu) { JáAnunciada = jáAnunciada };

    // Pede pro yt-audio preparar a próxima — o link e a capa — pra ela começar sem buraco.
    internal void PrepararPróxima()
    {
        var próxima = Queue.Peek()?.Track;

        _youtube?.Preparar(próxima);
        CapaDaFaixa.Adiantar(próxima, _logger);
    }

    protected override async ValueTask NotifyTrackStartedAsync(
        ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackStartedAsync(track, cancellationToken);

        Fotografar();
        PrepararPróxima();

        if (track is FaixaPedida { JáAnunciada: true } pedida)
        {
            pedida.JáAnunciada = false;
            return;
        }

        if (track.Track is not { } faixa)
            return;

        var cartão = await Music.CartãoTocandoAsync(
            faixa, Music.QuemPediu(track), Music.Próxima(this), _logger, cancellationToken);

        await FalarAsync(cartão, cancellationToken);
    }

    // Guarda a foto de agora: a biblioteca não me avisa do que fazem com a fila.
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

    // A faixa morreu no meio — fonte bloqueada, região. Uma eu pulo calada; a sequência eu corto.
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

    // Fim que não é falha zera a contagem: o que me interessa é sequência, não total.
    protected override async ValueTask NotifyTrackEndedAsync(
        ITrackQueueItem track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        if (endReason is not TrackEndReason.LoadFailed)
            _falhasSeguidas = 0;

        await base.NotifyTrackEndedAsync(track, endReason, cancellationToken);

        // a fila andou (ou acabou): a foto anterior ainda mostrava esta faixa como atual
        Fotografar();
    }

    // Saí do canal: não tem o que retomar, e a foto só serve pra restart no meio da música.
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

    // O Lavalink pode mandar a falha sem texto nenhum; aí eu digo isso mesmo.
    private static string Motivo(TrackException exception) =>
        exception.Message is { Length: > 0 } texto ? texto : "sem motivo";

    // Fala no canal onde o player nasceu. Canal apagado não derruba a música: vira log.
    internal ValueTask FalarAsync(EmbedProperties embed, CancellationToken cancellationToken) =>
        FalarAsync(new MessageProperties { Embeds = [embed] }, cancellationToken);

    // O anúncio no formato novo, com a capa grande dentro do card.
    internal ValueTask FalarAsync(Cartão cartão, CancellationToken cancellationToken) =>
        FalarAsync(
            new MessageProperties
            {
                Components = [cartão.Container],
                Attachments = cartão.Anexo is null ? null : [cartão.Anexo],
                Flags = MessageFlags.IsComponentsV2,
            },
            cancellationToken);

    private async ValueTask FalarAsync(MessageProperties mensagem, CancellationToken cancellationToken)
    {
        if (_canal is null)
            return;

        try
        {
            await _canal.SendMessageAsync(mensagem, cancellationToken: cancellationToken);
        }
        catch (RestException ex)
        {
            _logger.LogWarning(
                ex, "Não consegui falar no canal {Channel} da guild {Guild}", _canal.Id, GuildId);
        }
    }
}

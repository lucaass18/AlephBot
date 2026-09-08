using AlephBot.Core.Personality;

using Lavalink4NET.InactivityTracking.Players;
using Lavalink4NET.InactivityTracking.Trackers;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;

using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// Uma faixa na fila com o nome de quem pediu junto. O <see cref="TrackQueueItem"/> da
/// biblioteca só carrega a faixa, e sem isto o "pedido por" some assim que a mensagem
/// do /play rola pra cima.
/// </summary>
public sealed class FaixaPedida : ITrackQueueItem
{
    public FaixaPedida(TrackReference reference, string quemPediu)
    {
        Reference = reference;
        QuemPediu = quemPediu;
    }

    public TrackReference Reference { get; }

    public string QuemPediu { get; }

    /// <summary>
    /// O comando que enfileirou esta faixa já mostrou ela na resposta, então o anúncio
    /// automático fica quieto quando ela começar — senão o canal leva duas mensagens
    /// iguais em sequência. Vale uma vez só: se a faixa voltar a tocar no loop, o
    /// anúncio volta a valer.
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
/// O player da fila com duas coisas a mais: ele anuncia sozinho a faixa que começou —
/// é o que faz a fila andar sem ninguém digitando nada — e se despede antes de sair
/// quando o rastreador de inatividade desiste do canal.
/// </summary>
public sealed class AlephPlayer : QueuedLavalinkPlayer, IInactivityPlayerListener
{
    private readonly TextChannel? _canal;
    private readonly ILogger<AlephPlayer> _logger;

    public AlephPlayer(IPlayerProperties<AlephPlayer, AlephPlayerOptions> properties)
        : base(properties)
    {
        _canal = properties.Options.Value.Canal;
        _logger = properties.Logger;
    }

    protected override async ValueTask NotifyTrackStartedAsync(
        ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackStartedAsync(track, cancellationToken);

        if (track is FaixaPedida { JáAnunciada: true } pedida)
        {
            pedida.JáAnunciada = false;
            return;
        }

        if (track.Track is not { } faixa)
            return;

        await FalarAsync(Music.EmbedTocando(faixa, Music.QuemPediu(track), Volume), cancellationToken);
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

    /// <summary>
    /// Fala no canal onde o player nasceu. Canal apagado ou permissão retirada não pode
    /// derrubar a música: o erro vira log e a faixa segue tocando.
    /// </summary>
    private async ValueTask FalarAsync(EmbedProperties embed, CancellationToken cancellationToken)
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

using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Rest.Entities.Usage;

using Microsoft.Extensions.Hosting;

namespace AlephBot.Threnodian.Api;

// O que eu sei do Lavalink sem perguntar: ele avisa e eu anoto, desde o construtor.
public sealed class LavalinkMonitor : IHostedService
{
    private const double Mb = 1024 * 1024;

    private readonly IAudioService _áudio;
    private readonly Lock _trava = new();

    // nasce "conectando": o Lavalink4NET tenta desde o boot, e segue tentando se ele cair
    private ConnectionStatus _status = ConnectionStatus.Connecting;
    private DateTimeOffset? _desde;
    private LavalinkServerStatistics? _números;
    private DateTimeOffset _númerosEm;

    public LavalinkMonitor(IAudioService áudio)
    {
        _áudio = áudio;

        _áudio.ConnectionReady += AoConectarAsync;
        _áudio.ConnectionClosed += AoCairAsync;
        _áudio.StatisticsUpdated += AoReceberNúmerosAsync;
    }

    public LavalinkStatus Status()
    {
        lock (_trava)
        {
            var números = _números is { } n
                ? new LavalinkStats(
                    n.ConnectedPlayers,
                    n.PlayingPlayers,
                    (long)n.Uptime.TotalSeconds,
                    Math.Round(n.MemoryUsage.UsedMemory / Mb, 1),
                    Math.Round(n.ProcessorUsage.LavalinkLoad, 4),
                    _númerosEm)
                : null;

            return new LavalinkStatus(_status, _desde, números);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _áudio.ConnectionReady -= AoConectarAsync;
        _áudio.ConnectionClosed -= AoCairAsync;
        _áudio.StatisticsUpdated -= AoReceberNúmerosAsync;

        return Task.CompletedTask;
    }

    private Task AoConectarAsync(object sender, ConnectionReadyEventArgs eventArgs)
    {
        Mudar(ConnectionStatus.Connected);
        return Task.CompletedTask;
    }

    // Caiu e vai tentar de novo é "conectando"; caiu pra ficar é "desconectado".
    private Task AoCairAsync(object sender, ConnectionClosedEventArgs eventArgs)
    {
        Mudar(eventArgs.AllowReconnect ? ConnectionStatus.Connecting : ConnectionStatus.Disconnected);
        return Task.CompletedTask;
    }

    private Task AoReceberNúmerosAsync(object sender, StatisticsUpdatedEventArgs eventArgs)
    {
        lock (_trava)
        {
            _números = eventArgs.Statistics;
            _númerosEm = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    private void Mudar(ConnectionStatus status)
    {
        lock (_trava)
        {
            _status = status;
            _desde = DateTimeOffset.UtcNow;

            // número de antes da queda descreve um Lavalink que não existe mais
            _números = null;
        }
    }
}

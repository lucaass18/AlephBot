using System.Diagnostics;
using System.Runtime.InteropServices;

using AlephBot.Config;
using AlephBot.Core.Commands;
using AlephBot.Threnodian.Activity;

using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;

using NetCord.Gateway;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// O que a API conta, lido na hora de cada pedido. Nada aqui é guardado nem calculado em
/// fundo: o cache do gateway, os players e o registry já são a fonte da verdade, e ler eles
/// custa menos que manter uma cópia em dia.
/// </summary>
public sealed class BotStatus
{
    private const double Mb = 1024 * 1024;

    // a idade do processo, não a deste objeto: ele nasce no primeiro pedido, o processo nasceu antes
    private static readonly DateTimeOffset Início = InícioDoProcesso();

    private readonly GatewayClient _client;
    private readonly IAudioService _áudio;
    private readonly CommandRegistry _registry;
    private readonly LavalinkMonitor _lavalink;
    private readonly AlephConfig _config;

    // a lista não muda com o bot de pé; montar de novo a cada pedido seria reflection à toa
    private readonly Lazy<IReadOnlyList<CommandResponse>> _comandos;

    public BotStatus(
        GatewayClient client,
        IAudioService áudio,
        CommandRegistry registry,
        LavalinkMonitor lavalink,
        AlephConfig config)
    {
        _client = client;
        _áudio = áudio;
        _registry = registry;
        _lavalink = lavalink;
        _config = config;
        _comandos = new Lazy<IReadOnlyList<CommandResponse>>(MontarComandos);
    }

    /// <summary>
    /// Sem Discord eu estou fora; sem Lavalink eu respondo, só não toco. A música cair não me
    /// derruba — /ping e /ban seguem funcionando — e o health diz isso como "degraded".
    /// </summary>
    public HealthResponse Saúde()
    {
        var discord = Discord();
        var lavalink = _lavalink.Status().Status;

        var status = discord != ConnectionStatus.Connected ? HealthStatus.Down
            : lavalink != ConnectionStatus.Connected ? HealthStatus.Degraded
            : HealthStatus.Ok;

        return new HealthResponse(status, discord, lavalink);
    }

    public StatusResponse Status()
    {
        var discord = Discord();

        return new StatusResponse(
            Bot: Identidade(),
            Version: AlephBot.Version,
            Runtime: RuntimeInformation.FrameworkDescription,
            Mode: _config.IsDevelopment ? RunMode.Development : RunMode.Production,
            Prefix: _config.Prefix,
            StartedAt: Início,
            UptimeSeconds: (long)(DateTimeOffset.UtcNow - Início).TotalSeconds,
            Discord: new DiscordStatus(
                discord,
                discord == ConnectionStatus.Connected ? (int)Math.Round(_client.Latency.TotalMilliseconds) : null),
            Lavalink: _lavalink.Status());
    }

    public StatsResponse Estatísticas()
    {
        var gente = UserCount.Of(_client);
        var players = _áudio.Players.Players.ToList();

        var música = new MusicStats(
            Players: players.Count,
            Playing: players.Count(player => player.State == PlayerState.Playing),
            Paused: players.Count(player => player.State == PlayerState.Paused),
            QueuedTracks: players.OfType<IQueuedLavalinkPlayer>().Sum(player => player.Queue.Count));

        var memória = new MemoryStats(
            WorkingSetMb: Math.Round(Environment.WorkingSet / Mb, 1),
            GcHeapMb: Math.Round(GC.GetTotalMemory(forceFullCollection: false) / Mb, 1));

        return new StatsResponse(gente.Guilds, gente.TotalUsers, gente.Online, Comandos().Count, música, memória);
    }

    public IReadOnlyList<CommandResponse> Comandos() => _comandos.Value;

    /// <summary>
    /// Slash e prefixo são classes separadas com o mesmo Name, como no detalhe do /help: aqui
    /// elas viram um item só, com as duas formas de chamar e os atalhos do prefixo.
    /// </summary>
    private IReadOnlyList<CommandResponse> MontarComandos()
    {
        var prefixo = _config.Prefix;

        return _registry.All
            .GroupBy(comando => comando.Name, StringComparer.OrdinalIgnoreCase)
            .Where(entradas => entradas.Any(comando => !comando.IsHidden))
            .Select(entradas =>
            {
                // a versão visível é a que descreve o comando; a escondida é só o gêmeo de prefixo
                var principal = entradas.First(comando => !comando.IsHidden);

                var atalhos = entradas
                    .SelectMany(comando => CommandAudit.AliasesDeTexto(comando.Type))
                    .Where(atalho => !string.Equals(atalho, principal.Name, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new CommandResponse(
                    principal.Name,
                    principal.Description,
                    principal.Category,
                    Slash: entradas.FirstOrDefault(comando => !comando.IsTexto)?.Uso(prefixo),
                    Text: entradas.FirstOrDefault(comando => comando.IsTexto)?.Uso(prefixo),
                    Aliases: atalhos);
            })
            .OrderBy(comando => comando.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private ConnectionStatus Discord() => _client.Status switch
    {
        WebSocketStatus.Ready => ConnectionStatus.Connected,
        WebSocketStatus.Connecting => ConnectionStatus.Connecting,
        _ => ConnectionStatus.Disconnected,
    };

    private BotIdentity? Identidade() =>
        _client.Cache.User is { } eu ? new BotIdentity(eu.Username) : null;

    private static DateTimeOffset InícioDoProcesso()
    {
        using var processo = Process.GetCurrentProcess();
        return processo.StartTime.ToUniversalTime();
    }
}

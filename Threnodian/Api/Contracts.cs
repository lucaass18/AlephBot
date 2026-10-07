using AlephBot.Core.Commands.Interface;

namespace AlephBot.Threnodian.Api;

// O formato do que sai pela API. Os nomes ficam em inglês porque viram as chaves do JSON, e
// quem consome API espera "guilds", não "servidores". IDs do Discord saem como texto: são
// inteiros de 64 bits, e o JavaScript arredonda número desse tamanho sem avisar ninguém.

public enum ConnectionStatus
{
    Connected,

    /// <summary>Ainda não conectou, ou caiu e está tentando de novo.</summary>
    Connecting,

    Disconnected,
}

public enum HealthStatus
{
    /// <summary>Discord e Lavalink de pé.</summary>
    Ok,

    /// <summary>Respondo comando, mas a música está fora — o Lavalink não está lá.</summary>
    Degraded,

    /// <summary>Sem Discord eu não respondo nada.</summary>
    Down,
}

public enum RunMode
{
    Production,

    /// <summary>Com DEV_GUILD_ID: os slash commands vão só pro servidor de testes.</summary>
    Development,
}

/// <summary>GET /api/health — a única rota sem chave.</summary>
public sealed record HealthResponse(
    HealthStatus Status,
    ConnectionStatus Discord,
    ConnectionStatus Lavalink);

/// <summary>GET /api/status — quem eu sou e se as minhas conexões estão de pé.</summary>
public sealed record StatusResponse(
    BotIdentity? Bot,
    string Version,
    string Runtime,
    RunMode Mode,
    string Prefix,
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    DiscordStatus Discord,
    LavalinkStatus Lavalink);

/// <summary>Null até o Discord mandar o READY — antes disso eu não sei nem meu nome.</summary>
public sealed record BotIdentity(
    string Id,
    string Username,
    string AvatarUrl);

/// <summary><see cref="LatencyMs"/> é null fora do ar: latência de conexão caída é número velho.</summary>
public sealed record DiscordStatus(
    ConnectionStatus Status,
    int? LatencyMs);

/// <summary>
/// <see cref="Since"/> é desde quando está no estado atual. <see cref="Stats"/> é o último
/// relatório do próprio Lavalink, que chega a cada minuto — null até o primeiro.
/// </summary>
public sealed record LavalinkStatus(
    ConnectionStatus Status,
    DateTimeOffset? Since,
    LavalinkStats? Stats);

public sealed record LavalinkStats(
    int Players,
    int PlayingPlayers,
    long UptimeSeconds,
    double MemoryUsedMb,
    double CpuLoad,
    DateTimeOffset ReportedAt);

/// <summary>
/// GET /api/stats. <see cref="Members"/> soma os membros de cada servidor — quem está em dois
/// conta duas vezes. <see cref="Online"/> não: são pessoas distintas, sem bots.
/// </summary>
public sealed record StatsResponse(
    int Guilds,
    int Members,
    int Online,
    int Commands,
    MusicStats Music,
    MemoryStats Memory);

public sealed record MusicStats(
    int Players,
    int Playing,
    int Paused,
    int QueuedTracks);

public sealed record MemoryStats(
    double WorkingSetMb,
    double GcHeapMb);

/// <summary>
/// GET /api/commands — um item por comando, com as duas formas de chamar juntas. O que o
/// /help esconde não aparece aqui também.
/// </summary>
public sealed record CommandResponse(
    string Name,
    string Description,
    CommandCategory Category,
    string? Slash,
    string? Text,
    IReadOnlyList<string> Aliases);

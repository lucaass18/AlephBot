using AlephBot.Core.Commands.Contratos;

namespace AlephBot.Threnodian.Api;

// O formato do que sai pela API. Nomes em inglês porque viram chaves do JSON; nenhum ID sai.

public enum ConnectionStatus
{
    Connected,

    // Ainda não conectou, ou caiu e está tentando de novo.
    Connecting,

    Disconnected,
}

public enum HealthStatus
{
    // Discord e Lavalink de pé.
    Ok,

    // Respondo comando, mas a música está fora — o Lavalink não está lá.
    Degraded,

    // Sem Discord eu não respondo nada.
    Down,
}

public enum RunMode
{
    Production,

    // Com DEV_GUILD_ID: os slash commands vão só pro servidor de testes.
    Development,
}

// GET /api/health — sem chave: é o que monitor de uptime chama.
public sealed record HealthResponse(
    HealthStatus Status,
    ConnectionStatus Discord,
    ConnectionStatus Lavalink);

// GET /api/status — quem eu sou e se as minhas conexões estão de pé. Sem chave.
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

// Null até o READY — antes disso não sei nem meu nome. Só o nome: a rota é aberta.
public sealed record BotIdentity(string Username);

// LatencyMs é null fora do ar: latência de conexão caída é número velho.
public sealed record DiscordStatus(
    ConnectionStatus Status,
    int? LatencyMs);

// Since é desde quando está assim; Stats é o último relatório do Lavalink, null até o primeiro.
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

// GET /api/stats. Members soma por servidor (quem está em dois conta duas vezes); Online não.
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

// GET /api/commands — um item por comando; o que o /help esconde também não aparece.
public sealed record CommandResponse(
    string Name,
    string Description,
    CommandCategory Category,
    string? Slash,
    string? Text,
    IReadOnlyList<string> Aliases);

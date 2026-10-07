using AlephBot.Core.Commands.Interface;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// O que os docs mostram como resposta de cada rota, no lugar de "string" e 0. São objetos de
/// verdade dos contratos: se uma resposta mudar de formato, o exemplo para de compilar em vez
/// de ficar mentindo. Os comandos são dois que existem mesmo, com a descrição que o /help dá.
/// </summary>
internal static class ExemplosDaApi
{
    private static readonly DateTimeOffset Subiu = new(2026, 10, 7, 12, 48, 42, TimeSpan.Zero);

    public static readonly HealthResponse Saúde =
        new(HealthStatus.Ok, ConnectionStatus.Connected, ConnectionStatus.Connected);

    public static readonly StatusResponse Status = new(
        Bot: new BotIdentity("AlephBot"),
        Version: AlephBot.Version,
        Runtime: ".NET 10.0.12",
        Mode: RunMode.Production,
        Prefix: "!",
        StartedAt: Subiu,
        UptimeSeconds: 3600,
        Discord: new DiscordStatus(ConnectionStatus.Connected, LatencyMs: 42),
        Lavalink: new LavalinkStatus(
            ConnectionStatus.Connected,
            Since: Subiu.AddSeconds(8),
            Stats: new LavalinkStats(
                Players: 1,
                PlayingPlayers: 1,
                UptimeSeconds: 3590,
                MemoryUsedMb: 180.2,
                CpuLoad: 0.012,
                ReportedAt: Subiu.AddSeconds(3578))));

    public static readonly StatsResponse Estatísticas = new(
        Guilds: 12,
        Members: 3456,
        Online: 210,
        Commands: 21,
        Music: new MusicStats(Players: 2, Playing: 1, Paused: 1, QueuedTracks: 17),
        Memory: new MemoryStats(WorkingSetMb: 142.7, GcHeapMb: 38.1));

    public static readonly CommandResponse[] Comandos =
    [
        new("ping", "Mostra a latência do bot.", CommandCategory.Utility, "/ping", "!ping", []),
        new("play", "Toca uma música ou põe ela na fila.", CommandCategory.Music,
            "/play <nome ou link>", "!play <nome ou link>", ["p", "tocar"]),
    ];
}

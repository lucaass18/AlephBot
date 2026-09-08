using AlephBot.Config;
using AlephBot.Core.Personality;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Gateway;

namespace AlephBot.Threnodian.Activity;

/// <summary>
/// Mantém a atividade do bot atualizada com a contagem de usuários online.
/// Ex.: "Playing 12 online | !help"
/// </summary>
public sealed class BotActivityService : BackgroundService
{
    // o cache de guilds só enche depois dos GUILD_CREATE, que chegam após o READY
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly GatewayClient _client;
    private readonly AlephConfig _config;
    private readonly ILogger<BotActivityService> _logger;

    public BotActivityService(
        GatewayClient client,
        AlephConfig config,
        ILogger<BotActivityService> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);

            using var timer = new PeriodicTimer(RefreshInterval);

            do
            {
                await UpdateAsync();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutdown normal, não é erro
        }
    }

    private async Task UpdateAsync()
    {
        var count = CountUsers();

        // sem guild em cache o número não significa nada ainda; espera o próximo tick
        if (count.Guilds == 0)
        {
            _logger.LogInformation("Cache de guilds ainda vazio, pulando atualização de presença");
            return;
        }

        var text = BuildActivityText(count.Online);

        var presence = new PresenceProperties(UserStatusType.Online)
        {
            Activities = [new UserActivityProperties(text, UserActivityType.Playing)],
        };

        try
        {
            await _client.UpdatePresenceAsync(presence);
            _logger.LogInformation("Presença atualizada: {Activity}", text);
        }
        catch (Exception ex)
        {
            // falha de presença nunca deve derrubar o bot
            _logger.LogWarning(ex, "Falha ao atualizar a presença");
        }
    }

    private string BuildActivityText(int onlineCount) =>
        $"{Denia.PresençaVerbo()} {onlineCount:N0} online | {_config.Prefix}help";

    private Count CountUsers()
    {
        var selfId = _client.Cache.User?.Id;
        var count = new Count();

        // o mesmo usuário aparece uma vez por guild compartilhada; só conta distintos
        var seen = new HashSet<ulong>();

        foreach (var guild in _client.Cache.Guilds.Values)
        {
            count.Guilds++;
            count.CachedUsers += guild.Users.Count;
            count.TotalUsers += guild.UserCount;

            foreach (var (userId, presence) in guild.Presences)
            {
                count.Presences++;

                // Invisible chega como Offline, então não é contado — que é o certo
                if (presence.Status == UserStatusType.Offline)
                {
                    count.Offline++;
                    continue;
                }

                // dedupe antes de classificar: cada usuário é avaliado uma única vez,
                // então um bot não escapa por estar sem cache numa das guilds
                if (!seen.Add(userId))
                {
                    count.Duplicates++;
                    continue;
                }

                if (userId == selfId)
                {
                    count.Bots++;
                    continue;
                }

                // só dá pra saber se é bot se o membro estiver em cache
                if (guild.Users.TryGetValue(userId, out var user) && user.IsBot)
                {
                    count.Bots++;
                    continue;
                }

                count.Online++;
            }
        }

        _logger.LogDebug(
            "Contagem | guilds: {Guilds} | presences: {Presences} | membros no cache: {Cached}/{Total} | offline: {Offline} | duplicados: {Duplicates} | bots+self: {Bots} | online: {Online}",
            count.Guilds, count.Presences, count.CachedUsers, count.TotalUsers, count.Offline, count.Duplicates, count.Bots, count.Online);

        return count;
    }

    private struct Count
    {
        public int Guilds;
        public int Presences;
        public int CachedUsers;
        public int TotalUsers;
        public int Offline;
        public int Duplicates;
        public int Bots;
        public int Online;
    }
}

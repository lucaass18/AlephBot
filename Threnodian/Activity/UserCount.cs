using NetCord;
using NetCord.Gateway;

namespace AlephBot.Threnodian.Activity;

/// <summary>
/// Quem eu enxergo agora, contado do cache do gateway. A presença mostra o
/// <see cref="Online"/> e a API mostra o resto; os outros campos existem pra explicar o
/// Online quando ele parecer errado — é o que sai no log de debug.
/// </summary>
public struct UserCount
{
    public int Guilds;
    public int Presences;
    public int CachedUsers;
    public int TotalUsers;
    public int Offline;
    public int Duplicates;
    public int Bots;
    public int Online;

    public static UserCount Of(GatewayClient client)
    {
        var selfId = client.Cache.User?.Id;
        var count = new UserCount();

        // o mesmo usuário aparece uma vez por guild compartilhada; só conta distintos
        var seen = new HashSet<ulong>();

        foreach (var guild in client.Cache.Guilds.Values)
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

                // dedupe antes de classificar, senão um bot escapa por não estar em cache
                // numa das guilds e acaba entrando na conta
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

        return count;
    }
}

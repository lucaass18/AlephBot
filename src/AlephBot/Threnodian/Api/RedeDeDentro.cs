using System.Net;

using Microsoft.AspNetCore.Http;

namespace AlephBot.Threnodian.Api;

// Quem já está do lado de dentro: o contêiner, a rede do Docker, a de casa e a do Tailscale.
public static class RedeDeDentro
{
    public static readonly IReadOnlyList<IPNetwork> Redes =
    [
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("fc00::/7"),
    ];

    public static bool Contém(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        return Redes.Any(rede => rede.Contains(ip));
    }

    // O Tailscale marca tudo o que entra pela internet, pelo Funnel.
    public static bool PeloFunnel(HttpContext http) =>
        http.Request.Headers.ContainsKey("Tailscale-Funnel-Request");

    // De dentro é sem a marca do Funnel e com IP daqui; o X-Forwarded-For é lido antes.
    public static bool ÉDeDentro(HttpContext http) =>
        !PeloFunnel(http)
        && http.Connection.RemoteIpAddress is { } ip
        && Contém(ip);
}

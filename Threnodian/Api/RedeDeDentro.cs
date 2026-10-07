using System.Net;

using Microsoft.AspNetCore.Http;

namespace AlephBot.Threnodian.Api;

/// <summary>
/// Quem já está do lado de dentro: o próprio contêiner, a rede do Docker (é por ela que chega o
/// que vem do Tailscale e do túnel SSH), a rede de casa e a do Tailscale. Quem vem de fora —
/// pelo Funnel ou com a porta aberta direto pra internet (API_BIND=0.0.0.0) — tem IP público.
/// </summary>
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

    /// <summary>O Tailscale marca tudo o que entra pela internet, pelo Funnel.</summary>
    public static bool PeloFunnel(HttpContext http) =>
        http.Request.Headers.ContainsKey("Tailscale-Funnel-Request");

    /// <summary>
    /// O pedido veio de dentro: sem a marca do Funnel e de um IP daqui. O IP já é o de quem
    /// pediu de verdade — o X-Forwarded-For do Tailscale é lido antes, e só vale vindo de dentro.
    /// </summary>
    public static bool ÉDeDentro(HttpContext http) =>
        !PeloFunnel(http)
        && http.Connection.RemoteIpAddress is { } ip
        && Contém(ip);
}

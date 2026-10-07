using System.Net;

using AlephBot.Threnodian.Api;

namespace AlephBot.Tests.Api;

public class RedeDeDentroTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.18.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.10")]
    [InlineData("100.64.0.1")]
    [InlineData("100.94.84.30")]
    [InlineData("::1")]
    [InlineData("fd7a:115c:a1e0::1")]
    [InlineData("::ffff:172.18.0.1")]
    public void Loopback_Docker_casa_e_Tailscale_são_de_dentro(string ip)
    {
        Assert.True(RedeDeDentro.Contém(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("203.0.113.9")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("::ffff:8.8.8.8")]
    public void IP_público_é_de_fora(string ip)
    {
        Assert.False(RedeDeDentro.Contém(IPAddress.Parse(ip)));
    }
}

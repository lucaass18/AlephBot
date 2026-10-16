using System.Net;

using AlephBot.Threnodian.Api;

namespace AlephBot.Tests.Api;

// Os headers de proteção. O 429 gasta cota, então cada teste sobe a própria API.
public class ProtecaoDeForaTests
{
    private static readonly IPAddress IpDeFora = IPAddress.Parse("203.0.113.77");

    [Theory]
    [InlineData("/api/v1/openapi.json")]
    [InlineData("/api/v1/status")]
    [InlineData("/api/v1/health")]
    public async Task Ninguém_embute_a_API_em_outro_site(string rota)
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (_, _, headers) = await api.GetComHeadersAsync(rota, IpDeFora);

        Assert.Equal("DENY", headers.XFrameOptions);
        Assert.Equal("frame-ancestors 'none'", headers.ContentSecurityPolicy);
        Assert.Equal("nosniff", headers.XContentTypeOptions);
        Assert.Equal("no-referrer", headers["Referrer-Policy"]);
    }

    [Fact]
    public async Task Até_o_429_sai_com_os_headers_de_proteção()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        for (var i = 0; i < LimiteDePedidos.DeFora; i++)
            await api.GetAsync("/api/v1/status", IpDeFora);

        var (status, _, headers) = await api.GetComHeadersAsync("/api/v1/status", IpDeFora);

        Assert.Equal(429, status);
        Assert.Equal("DENY", headers.XFrameOptions);
    }
}

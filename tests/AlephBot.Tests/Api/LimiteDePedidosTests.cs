using System.Net;
using System.Text.Json.Nodes;

using AlephBot.Core.Personality;
using AlephBot.Threnodian.Api;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Api;

/// <summary>
/// O limite de pedidos. Cada teste sobe a própria API: o limite guarda estado, e um teste não
/// pode gastar a cota do outro. Os nomes dos headers ficam escritos por extenso de propósito:
/// são contrato com quem usa a API, e mudar um deles tem que quebrar teste.
/// </summary>
public class LimiteDePedidosTests
{
    private static readonly IPAddress IpDeFora = IPAddress.Parse("203.0.113.77");
    private static readonly IPAddress IpDeDentro = IPAddress.Parse("100.94.84.30");

    [Fact]
    public async Task Cada_resposta_conta_a_cota_e_quanto_sobra()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (_, _, primeira) = await api.GetComHeadersAsync("/api/v1/status", IpDeFora);
        var (_, _, segunda) = await api.GetComHeadersAsync("/api/v1/status", IpDeFora);

        Assert.Equal("60", primeira["X-RateLimit-Limit"]);
        Assert.Equal("59", primeira["X-RateLimit-Remaining"]);
        Assert.Equal("58", segunda["X-RateLimit-Remaining"]);
    }

    [Fact]
    public async Task De_fora_quem_passa_do_limite_espera()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        for (var i = 0; i < LimiteDePedidos.DeFora; i++)
        {
            var (status, _) = await api.GetAsync("/api/v1/status", IpDeFora);
            Assert.Equal(200, status);
        }

        var (bloqueado, corpo, headers) = await api.GetComHeadersAsync("/api/v1/status", IpDeFora);

        Assert.Equal(429, bloqueado);
        Assert.Equal("60", headers.RetryAfter);
        Assert.Equal("0", headers["X-RateLimit-Remaining"]);
        Assert.StartsWith("application/problem+json", headers.ContentType.ToString());
        Assert.Equal(Denia.ApiPedidosDemais(60), (string?)JsonNode.Parse(corpo)!["detail"]);
    }

    [Fact]
    public async Task O_limite_é_por_IP()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        for (var i = 0; i <= LimiteDePedidos.DeFora; i++)
            await api.GetAsync("/api/v1/status", IpDeFora);

        var (outroIp, _) = await api.GetAsync("/api/v1/status", IPAddress.Parse("198.51.100.1"));

        Assert.Equal(200, outroIp);
    }

    [Fact]
    public async Task No_IPv6_a_casa_inteira_divide_a_cota()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        // um endereço diferente a cada pedido, todos no mesmo /64
        for (var i = 1; i <= LimiteDePedidos.DeFora; i++)
            await api.GetAsync("/api/v1/status", IPAddress.Parse($"2001:db8:1:2::{i:x}"));

        var (mesmaCasa, _) = await api.GetAsync("/api/v1/status", IPAddress.Parse("2001:db8:1:2:ffff::1"));
        var (outraCasa, _) = await api.GetAsync("/api/v1/status", IPAddress.Parse("2001:db8:1:3::1"));

        Assert.Equal(429, mesmaCasa);
        Assert.Equal(200, outraCasa);
    }

    [Fact]
    public async Task De_dentro_a_cota_é_maior_mas_existe()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (_, _, headers) = await api.GetComHeadersAsync("/api/v1/status", IpDeDentro);
        Assert.Equal("600", headers["X-RateLimit-Limit"]);

        for (var i = 1; i < LimiteDePedidos.DeDentro; i++)
        {
            var (status, _) = await api.GetAsync("/api/v1/status", IpDeDentro);
            Assert.Equal(200, status);
        }

        var (bloqueado, _) = await api.GetAsync("/api/v1/status", IpDeDentro);
        Assert.Equal(429, bloqueado);
    }

    [Fact]
    public async Task Pelo_Funnel_vale_a_cota_de_fora_mesmo_vindo_da_rede_de_dentro()
    {
        // o Funnel entrega pelo localhost, e é a marca dele que diz que o pedido veio de fora
        await using var api = await ApiDeTeste.SubirAsync();

        var (_, _, headers) = await api.GetComHeadersAsync(
            "/api/v1/status",
            headers: [("Tailscale-Funnel-Request", "?1"), ("X-Forwarded-For", "203.0.113.9")]);

        Assert.Equal("60", headers["X-RateLimit-Limit"]);
    }

    [Fact]
    public async Task Chave_errada_também_gasta_a_cota()
    {
        await using var api = await ApiDeTeste.SubirAsync();

        var (status, _, headers) = await api.GetComHeadersAsync(
            "/api/v1/stats",
            IpDeFora,
            headers: (ApiKeyFilter.Header, "chave-chutada-1234567890"));

        Assert.Equal(401, status);
        Assert.Equal("59", headers["X-RateLimit-Remaining"]);
    }

    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    public void Quem_divide_a_cota_é_o_IP_e_no_IPv6_o_64(string ip, string cliente) =>
        Assert.Equal(cliente, LimiteDePedidos.ClienteDe(IPAddress.Parse(ip)));

    [Fact]
    public void A_recusa_vai_pro_log_uma_vez_por_minuto_por_IP()
    {
        using var limite = new LimiteDePedidos(NullLogger<LimiteDePedidos>.Instance);

        Assert.True(limite.DeveAnotar("203.0.113.77", agoraEmMs: 0));
        Assert.False(limite.DeveAnotar("203.0.113.77", agoraEmMs: 30_000));
        Assert.True(limite.DeveAnotar("198.51.100.1", agoraEmMs: 30_000));
        Assert.True(limite.DeveAnotar("203.0.113.77", agoraEmMs: 60_000));
    }
}

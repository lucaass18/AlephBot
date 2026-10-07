using AlephBot.Config;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.Api;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Api;

/// <summary>O filtro sozinho, sem servidor: o que passa, o que volta com 401 e com qual frase.</summary>
public class ApiKeyFilterTests
{
    private const string ChaveCerta = "chave-certa-1234567890";
    private const string Passou = "passou";

    private static async Task<object?> ChamarAsync(string? chaveEnviada)
    {
        var filtro = new ApiKeyFilter(new ApiConfig(ChaveCerta, 8080), NullLogger<ApiKeyFilter>.Instance);
        var http = new DefaultHttpContext();

        if (chaveEnviada is not null)
            http.Request.Headers[ApiKeyFilter.Header] = chaveEnviada;

        return await filtro.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(http),
            _ => ValueTask.FromResult<object?>(Passou));
    }

    private static ProblemHttpResult Recusa(object? resultado)
    {
        var problema = Assert.IsType<ProblemHttpResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, problema.StatusCode);
        return problema;
    }

    [Fact]
    public async Task Chave_certa_passa()
    {
        Assert.Equal(Passou, await ChamarAsync(ChaveCerta));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Sem_chave_volta_401_pedindo_a_chave(string? enviada)
    {
        var problema = Recusa(await ChamarAsync(enviada));

        Assert.Equal(Denia.ApiSemChave(ApiKeyFilter.Header), problema.ProblemDetails.Detail);
    }

    [Theory]
    [InlineData("chave-errada-1234567890")]
    [InlineData("CHAVE-CERTA-1234567890")]
    [InlineData("chave-certa-123456789")]
    [InlineData("chave-certa-1234567890 ")]
    public async Task Chave_diferente_em_qualquer_detalhe_volta_401(string enviada)
    {
        var problema = Recusa(await ChamarAsync(enviada));

        Assert.Equal(Denia.ApiChaveErrada(), problema.ProblemDetails.Detail);
    }
}

using AlephBot.Threnodian.Youtube;

namespace AlephBot.Tests.Youtube;

/// <summary>
/// Só a recusa que vem do Google pede login novo; tropeço de rede entre o Lavalink e ele não
/// pode virar um pedido de código no meio da madrugada.
/// </summary>
public class LavalinkYoutubeTests
{
    [Theory]
    [InlineData("500: Refreshing access token returned error invalid_grant")]
    [InlineData("400: invalid_grant")]
    [InlineData("500: Refreshing access token returned error unauthorized_client")]
    public void Recusa_do_Google_é_token_morto(string motivo)
    {
        Assert.True(LavalinkYoutube.Entrega.Recusado(motivo).TokenMorreu);
    }

    [Theory]
    [InlineData("500: Internal Server Error")]
    [InlineData("503: Service Unavailable")]
    public void Outra_recusa_não_é_token_morto(string motivo)
    {
        Assert.False(LavalinkYoutube.Entrega.Recusado(motivo).TokenMorreu);
    }

    [Fact]
    public void Entrega_aceita_ou_sem_Lavalink_não_é_token_morto()
    {
        Assert.False(LavalinkYoutube.Entrega.Aceito.TokenMorreu);
        Assert.False(LavalinkYoutube.Entrega.Indisponível.TokenMorreu);
    }
}

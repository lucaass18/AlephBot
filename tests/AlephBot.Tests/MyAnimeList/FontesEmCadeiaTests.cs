using System.Net;

using AlephBot.Threnodian.MyAnimeList;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.MyAnimeList;

/// <summary>
/// O /ma tinha uma fonte só, e quando ela morreu o comando morreu junto. A cadeia existe
/// pra isso não se repetir: a reserva responde quando a primeira não responde.
/// </summary>
public class FontesEmCadeiaTests
{
    private static readonly Obra Achado = new()
    {
        Tipo = TipoDeObra.Anime,
        Id = 20,
        Título = "NARUTO",
    };

    private static FontesEmCadeia Cadeia(params IMyAnimeList[] fontes) =>
        new(NullLogger<FontesEmCadeia>.Instance, fontes);

    [Fact]
    public async Task Usa_a_primeira_fonte_que_responde()
    {
        var reserva = new FonteFalsa { Resposta = [Achado] };

        var cadeia = Cadeia(new FonteFalsa { Resposta = [Achado] }, reserva);

        var obras = await cadeia.ProcurarAsync(TipoDeObra.Anime, "naruto", TestContext.Current.CancellationToken);

        Assert.Single(obras);
        Assert.Equal(0, reserva.Chamadas);
    }

    [Fact]
    public async Task Cai_pra_reserva_quando_a_primeira_falha()
    {
        var reserva = new FonteFalsa { Resposta = [Achado] };
        var cadeia = Cadeia(new FonteFalsa { Erro = new HttpRequestException("caiu") }, reserva);

        var obras = await cadeia.ProcurarAsync(TipoDeObra.Anime, "naruto", TestContext.Current.CancellationToken);

        Assert.Single(obras);
        Assert.Equal(1, reserva.Chamadas);
    }

    /// <summary>Fonte viva que não conhece a obra é tão inútil quanto fonte morta.</summary>
    [Fact]
    public async Task Cai_pra_reserva_quando_a_primeira_não_acha_nada()
    {
        var reserva = new FonteFalsa { Resposta = [Achado] };

        var cadeia = Cadeia(new FonteFalsa { Resposta = [] }, reserva);

        var obras = await cadeia.ProcurarAsync(TipoDeObra.Anime, "naruto", TestContext.Current.CancellationToken);

        Assert.Single(obras);
        Assert.Equal(1, reserva.Chamadas);
    }

    /// <summary>
    /// Client ID errado é erro meu, não queda do MAL: tem que chegar na cara de quem
    /// configurou, e não ser escondido por uma reserva que responde.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Credencial_recusada_não_cai_pra_reserva(HttpStatusCode status)
    {
        var reserva = new FonteFalsa { Resposta = [Achado] };
        var recusada = new FonteFalsa { Erro = new HttpRequestException("recusou", null, status) };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Cadeia(recusada, reserva).ProcurarAsync(TipoDeObra.Anime, "naruto", TestContext.Current.CancellationToken));

        Assert.Equal(0, reserva.Chamadas);
    }

    /// <summary>Todas caíram: quem chamou precisa saber que foi queda, e não "não achei".</summary>
    [Fact]
    public async Task Sobe_a_falha_quando_todas_as_fontes_falham()
    {
        var cadeia = Cadeia(
            new FonteFalsa { Erro = new HttpRequestException("caiu") },
            new FonteFalsa { Erro = new TaskCanceledException("demorou") });

        await Assert.ThrowsAnyAsync<Exception>(() => cadeia.ProcurarAsync(TipoDeObra.Anime, "naruto", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ninguém_achou_é_lista_vazia_e_não_erro()
    {
        var cadeia = Cadeia(new FonteFalsa { Resposta = [] }, new FonteFalsa { Resposta = [] });

        Assert.Empty(await cadeia.ProcurarAsync(TipoDeObra.Anime, "coisa que não existe", TestContext.Current.CancellationToken));
    }

    private sealed class FonteFalsa : IMyAnimeList
    {
        public IReadOnlyList<Obra> Resposta { get; init; } = [];

        public Exception? Erro { get; init; }

        public int Chamadas { get; private set; }

        public string Nome => "Falsa";

        public Task<IReadOnlyList<Obra>> ProcurarAsync(
            TipoDeObra tipo, string busca, CancellationToken cancellationToken = default)
        {
            Chamadas++;

            return Erro is not null
                ? Task.FromException<IReadOnlyList<Obra>>(Erro)
                : Task.FromResult(Resposta);
        }
    }
}

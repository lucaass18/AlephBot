using AlephBot.Core.Commands.Anime;
using AlephBot.Threnodian.MyAnimeList;

namespace AlephBot.Tests.Commands;

// Conferência manual contra o AniList de verdade: é onde título gigante, obra sem nota e
// sinopse enorme aparecem. Fica parado sem ALEPH_CARTAO_REAL, porque a suíte não usa rede.
public class CartaoRealTests
{
    private static readonly string[] Buscas =
    [
        "frieren",
        "one piece",
        "kaguya sama wa kokurasetai",
        "monogatari",
        "bocchi the rock",
        "chainsaw man",
    ];

    [Fact]
    public async Task Fichas_de_verdade_viram_cartão()
    {
        if (Environment.GetEnvironmentVariable("ALEPH_CARTAO_REAL") is not { Length: > 0 } pasta)
            return;

        Directory.CreateDirectory(pasta);

        using var fonte = new AniList();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var cancelamento = TestContext.Current.CancellationToken;

        foreach (var busca in Buscas)
        {
            var achados = await fonte.ProcurarAsync(TipoDeObra.Anime, busca, cancelamento);

            Assert.NotEmpty(achados);

            var obra = achados[0];
            byte[]? capa = null;

            if (obra.Imagem is { Length: > 0 } endereço)
                capa = await http.GetByteArrayAsync(endereço, cancelamento);

            var png = CartãoDaFicha.Png(obra, obra.Sinopse, capa);

            Assert.NotNull(png);

            var arquivo = Path.Combine(pasta, busca.Replace(' ', '-') + ".png");

            await File.WriteAllBytesAsync(arquivo, png!, cancelamento);

            // o AniList pede calma entre pedidos
            await Task.Delay(TimeSpan.FromMilliseconds(800), cancelamento);
        }
    }
}

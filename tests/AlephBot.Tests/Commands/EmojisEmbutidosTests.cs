using AlephBot.Threnodian.Emojis;

namespace AlephBot.Tests.Commands;

// Os PNGs que viajam dentro do dll. É com eles que o bot se vira sozinho no primeiro boot,
// sem ninguém arrastar arquivo no Developer Portal.
public class EmojisEmbutidosTests
{
    private static readonly string[] Esperados =
    [
        "ma_autoria", "ma_elenco", "ma_estudio", "ma_formato", "ma_generos",
        "ma_nota", "ma_periodo", "ma_ranking", "ma_situacao",
    ];

    // Se o csproj parar de embutir, isto quebra aqui e não calado na VPS.
    [Fact]
    public void Os_nove_emoji_estão_no_assembly()
    {
        Assert.Equal(Esperados, EmojisEmbutidos.Nomes());
    }

    [Fact]
    public void Cada_um_traz_png_de_verdade()
    {
        foreach (var nome in Esperados)
        {
            var png = EmojisEmbutidos.Bytes(nome);

            Assert.NotNull(png);
            Assert.Equal<byte[]>([0x89, 0x50, 0x4E, 0x47], png![..4]);

            // o Discord recusa acima de 256 KB
            Assert.True(png.Length < 256 * 1024, $"{nome} tem {png.Length} bytes");
        }
    }

    [Fact]
    public void Emoji_que_não_existe_não_inventa_bytes()
    {
        Assert.Null(EmojisEmbutidos.Bytes("ma_inexistente"));
    }

    // App vazio é o primeiro boot: sobe todos.
    [Fact]
    public void Com_o_app_vazio_faltam_todos()
    {
        Assert.Equal(Esperados, EmojisEmbutidos.QuaisFaltam([]));
    }

    // App já abastecido é todo boot depois do primeiro: não sobe nada, não duplica nada.
    [Fact]
    public void Com_o_app_completo_não_falta_nenhum()
    {
        Assert.Empty(EmojisEmbutidos.QuaisFaltam(Esperados));
    }

    [Fact]
    public void Falta_só_o_que_o_app_não_tem()
    {
        var noApp = Esperados.Where(n => n != "ma_nota" && n != "ma_elenco").ToArray();

        Assert.Equal(["ma_elenco", "ma_nota"], EmojisEmbutidos.QuaisFaltam(noApp));
    }

    // Emoji de outra origem no app não confunde a conta.
    [Fact]
    public void Emoji_alheio_no_app_é_ignorado()
    {
        Assert.Equal(Esperados, EmojisEmbutidos.QuaisFaltam(["pepe", "kekw"]));
    }

    [Fact]
    public void O_data_uri_sai_no_formato_que_o_discord_pede()
    {
        var uri = EmojisEmbutidos.ParaDataUri([0x89, 0x50, 0x4E, 0x47]);

        Assert.StartsWith("data:image/png;base64,", uri);
        Assert.Equal(Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]), uri["data:image/png;base64,".Length..]);
    }
}

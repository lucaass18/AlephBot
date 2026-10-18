using AlephBot.Threnodian.Emojis;

namespace AlephBot.Tests.Commands;

// Os PNGs que viajam dentro do dll. É com eles que o bot se vira sozinho no primeiro boot,
// sem ninguém arrastar arquivo no Developer Portal.
public class EmojisEmbutidosTests
{
    // Os ícones dos rótulos da ficha.
    private static readonly string[] DaFicha =
    [
        "ma_autoria", "ma_elenco", "ma_estudio", "ma_formato", "ma_generos",
        "ma_nota", "ma_periodo", "ma_ranking", "ma_situacao",
    ];

    // Os de reação, que a Denia usa na fala. Grandes demais pra rótulo, bons numa frase.
    private static readonly string[] DaDenia =
    [
        "idol_jidan", "idol_laco", "idol_oshi", "idol_suki", "idol_varinha",
        "pastel_abraco", "pastel_adora", "pastel_himitsu", "pastel_look5",
        "pastel_look6", "pastel_segredo", "pastel_surta", "pastel_torce",
        "popuko_aiyee", "popuko_angry", "popuko_aww", "popuko_cool", "popuko_heckyea",
        "popuko_hiss", "popuko_interdasting", "popuko_nyeh", "popuko_oopsie",
        "popuko_pat", "popuko_pekpek", "popuko_screenie", "popuko_shock", "popuko_sob",
        "popuko_strongshy", "popuko_uguu", "popuko_urgh", "popuko_whale",
        "popuko_wutyousay", "kanna_drink", "kanna_uhh", "catgirl_blanket",
    ];

    // Os animados. O tipo tem que sair gif, senão o Discord sobe o quadro parado.
    private static readonly string[] Animados = ["catgirl_cry", "catgirl_mad", "catgirl_okay"];

    private static readonly string[] Esperados =
        [.. DaFicha.Concat(DaDenia).Concat(Animados).Order(StringComparer.Ordinal)];

    // Se o csproj parar de embutir, isto quebra aqui e não calado na VPS.
    [Fact]
    public void Os_dois_grupos_estão_no_assembly()
    {
        Assert.Equal(Esperados, EmojisEmbutidos.Nomes());
    }

    [Fact]
    public void Cada_um_traz_imagem_de_verdade()
    {
        foreach (var nome in Esperados)
        {
            var imagem = EmojisEmbutidos.Bytes(nome);

            Assert.NotNull(imagem);

            var tipo = EmojisEmbutidos.Tipo(imagem!);
            byte[] assinatura = tipo == "image/gif" ? [(byte)'G', (byte)'I', (byte)'F'] : [0x89, 0x50, 0x4E, 0x47];

            Assert.Equal(assinatura, imagem![..assinatura.Length]);

            // o Discord recusa acima de 256 KB
            Assert.True(imagem.Length < 256 * 1024, $"{nome} tem {imagem.Length} bytes");
        }
    }

    // GIF entregue como png sobe parado, e isso só apareceria olhando o emoji no Discord.
    [Fact]
    public void Os_animados_saem_como_gif()
    {
        foreach (var nome in Animados)
        {
            var imagem = EmojisEmbutidos.Bytes(nome);

            Assert.NotNull(imagem);
            Assert.Equal("image/gif", EmojisEmbutidos.Tipo(imagem!));
            Assert.StartsWith("data:image/gif;base64,", EmojisEmbutidos.ParaDataUri(imagem!));
        }
    }

    [Fact]
    public void Os_parados_saem_como_png()
    {
        var imagem = EmojisEmbutidos.Bytes("ma_nota");

        Assert.NotNull(imagem);
        Assert.Equal("image/png", EmojisEmbutidos.Tipo(imagem!));
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
        var noApp = Esperados.Where(n => n != "ma_nota" && n != "pastel_surta").ToArray();

        Assert.Equal(["ma_nota", "pastel_surta"], EmojisEmbutidos.QuaisFaltam(noApp));
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

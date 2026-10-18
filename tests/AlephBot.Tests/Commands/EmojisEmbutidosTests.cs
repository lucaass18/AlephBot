using AlephBot.Threnodian.Emojis;

namespace AlephBot.Tests.Commands;

// Os PNGs e GIFs que viajam dentro do dll. É com eles que o bot se vira sozinho no primeiro
// boot, sem ninguém arrastar arquivo no Developer Portal.
public class EmojisEmbutidosTests
{
    // Os ícones dos rótulos da ficha. Estes vão listados porque o Denia.cs chama cada um pelo
    // nome: sumindo um, o rótulo perde o ícone calado.
    private static readonly string[] DaFicha =
    [
        "ma_autoria", "ma_elenco", "ma_estudio", "ma_formato", "ma_generos",
        "ma_nota", "ma_periodo", "ma_ranking", "ma_situacao",
    ];

    // Os que a Denia cita nas falas de recusa. Mesmo motivo: o nome está no código.
    private static readonly string[] DasFalas =
    [
        "kanna_uhh", "popuko_oopsie", "popuko_pat", "popuko_sob",
        "popuko_urgh", "popuko_wutyousay",
    ];

    // O resto é acervo de reação, e cresce a cada pacote que entra. Por isso daqui pra baixo a
    // conferência é por propriedade, e não por lista — senão todo pacote novo mexe no teste.
    private static IReadOnlyList<string> Todos => EmojisEmbutidos.Nomes();

    [Fact]
    public void Os_ícones_da_ficha_estão_no_assembly()
    {
        Assert.All(DaFicha, nome => Assert.Contains(nome, Todos));
    }

    // Renomear um arquivo sem mexer no Denia.cs tiraria o emoji da fala sem avisar ninguém.
    [Fact]
    public void Os_emoji_citados_nas_falas_existem()
    {
        Assert.All(DasFalas, nome => Assert.Contains(nome, Todos));
    }

    // Se o csproj parar de embutir, isto quebra aqui e não calado na VPS.
    [Fact]
    public void O_acervo_não_está_vazio()
    {
        Assert.True(Todos.Count >= DaFicha.Length + DasFalas.Length, $"só {Todos.Count} emoji");
    }

    // O Discord recusa nome fora disso, e a recusa só apareceria no log da VPS.
    [Fact]
    public void Todo_nome_serve_pro_discord()
    {
        Assert.All(Todos, nome =>
        {
            Assert.InRange(nome.Length, 2, 32);
            Assert.All(nome, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c == '_', $"{nome} tem '{c}'"));
        });
    }

    [Fact]
    public void Todo_emoji_traz_imagem_de_verdade()
    {
        Assert.All(Todos, nome =>
        {
            var imagem = EmojisEmbutidos.Bytes(nome);

            Assert.NotNull(imagem);

            byte[] assinatura = EmojisEmbutidos.Tipo(imagem!) == "image/gif"
                ? [(byte)'G', (byte)'I', (byte)'F']
                : [0x89, 0x50, 0x4E, 0x47];

            Assert.Equal(assinatura, imagem![..assinatura.Length]);
        });
    }

    [Fact]
    public void Nenhum_passa_do_limite_de_256_KB()
    {
        Assert.All(Todos, nome =>
        {
            var imagem = EmojisEmbutidos.Bytes(nome)!;

            Assert.True(imagem.Length < 256 * 1024, $"{nome} tem {imagem.Length} bytes");
        });
    }

    // GIF entregue como png sobe parado, e isso só apareceria olhando o emoji no Discord.
    [Fact]
    public void O_data_uri_combina_com_os_bytes()
    {
        Assert.All(Todos, nome =>
        {
            var imagem = EmojisEmbutidos.Bytes(nome)!;

            Assert.StartsWith($"data:{EmojisEmbutidos.Tipo(imagem)};base64,", EmojisEmbutidos.ParaDataUri(imagem));
        });
    }

    // Tem animado no acervo: se parar de ter, o suporte a GIF deixou de ser exercitado.
    [Fact]
    public void Existe_pelo_menos_um_animado()
    {
        Assert.Contains(Todos, nome => EmojisEmbutidos.Tipo(EmojisEmbutidos.Bytes(nome)!) == "image/gif");
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
        Assert.Equal(Todos, EmojisEmbutidos.QuaisFaltam([]));
    }

    // App já abastecido é todo boot depois do primeiro: não sobe nada, não duplica nada.
    [Fact]
    public void Com_o_app_completo_não_falta_nenhum()
    {
        Assert.Empty(EmojisEmbutidos.QuaisFaltam(Todos));
    }

    [Fact]
    public void Falta_só_o_que_o_app_não_tem()
    {
        var noApp = Todos.Where(n => n != "ma_nota" && n != "popuko_sob").ToArray();

        Assert.Equal(["ma_nota", "popuko_sob"], EmojisEmbutidos.QuaisFaltam(noApp));
    }

    // Emoji de outra origem no app não confunde a conta.
    [Fact]
    public void Emoji_alheio_no_app_é_ignorado()
    {
        Assert.Equal(Todos, EmojisEmbutidos.QuaisFaltam(["pepe", "kekw"]));
    }

    [Fact]
    public void O_data_uri_sai_no_formato_que_o_discord_pede()
    {
        var uri = EmojisEmbutidos.ParaDataUri([0x89, 0x50, 0x4E, 0x47]);

        Assert.StartsWith("data:image/png;base64,", uri);
        Assert.Equal(Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]), uri["data:image/png;base64,".Length..]);
    }
}

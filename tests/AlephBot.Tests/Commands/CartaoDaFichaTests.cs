using AlephBot.Core.Commands.Anime;
using AlephBot.Threnodian.MyAnimeList;

using SkiaSharp;

namespace AlephBot.Tests.Commands;

// A ficha desenhada: nome de campo de embed não aceita figura, então a ficha inteira vira imagem.
public class CartaoDaFichaTests
{
    private static Obra Ficha(string? sinopse = null, string? japonês = null) => new()
    {
        Fonte = "AniList",
        Tipo = TipoDeObra.Anime,
        Id = 154587,
        Título = "Sousou no Frieren",
        TítuloEmJaponês = japonês,
        Nota = 9.1,
        Episódios = 28,
        Início = new DataParcial { Ano = 2023 },
        Sinopse = sinopse,
        Gêneros = ["Aventura", "Drama", "Fantasia"],
        Personagens =
        [
            new Personagem { Nome = "Frieren", Principal = true },
            new Personagem { Nome = "Fern", Principal = true },
            new Personagem { Nome = "Stark" },
            new Personagem { Nome = "Himmel" },
        ],
    };

    // Uma capa qualquer, pra não depender de rede no teste. Com ALEPH_CARTAO_CAPA apontando
    // pra um arquivo, usa a capa de verdade — serve pra conferir o desenho com o olho.
    private static byte[] CapaFalsa()
    {
        if (Environment.GetEnvironmentVariable("ALEPH_CARTAO_CAPA") is { Length: > 0 } arquivo
            && File.Exists(arquivo))
            return File.ReadAllBytes(arquivo);

        using var superfície = SKSurface.Create(new SKImageInfo(460, 690));
        superfície.Canvas.Clear(new SKColor(0x3A, 0x6B, 0x5C));

        using var imagem = superfície.Snapshot();
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 90);

        return dados.ToArray();
    }

    [Fact]
    public void O_cartão_sai_em_png_do_tamanho_desenhado()
    {
        const string sinopse =
            "A aventura acabou, mas a vida continua para um mago elfo que está começando a "
            + "aprender o que é viver. A maga elfa Frieren e seus corajosos companheiros "
            + "derrotaram o Rei Demônio e trouxeram paz à terra. Mas Frieren sobreviveu por "
            + "muito tempo ao resto de seu antigo grupo. Décadas após a vitória, o funeral de "
            + "uma de suas amigas a confronta com sua quase imortalidade.";

        var obra = Ficha(sinopse, japonês: "葬送のフリーレン");
        var png = CartãoDaFicha.Png(obra, obra.Sinopse, CapaFalsa());

        Assert.NotNull(png);
        Assert.Equal<byte[]>([0x89, 0x50, 0x4E, 0x47], png![..4]);

        using var bitmap = SKBitmap.Decode(png);

        Assert.Equal(CartãoDaFicha.Largura, bitmap.Width);
        Assert.Equal(CartãoDaFicha.Altura, bitmap.Height);

        // pra conferir com o olho: ALEPH_CARTAO_SAIDA=C:\caminho\cartao.png dotnet test --project ...
        if (Environment.GetEnvironmentVariable("ALEPH_CARTAO_SAIDA") is { Length: > 0 } saída)
            File.WriteAllBytes(saída, png);
    }

    // Obra sem capa acontece: a fonte nem sempre tem imagem, e a ficha não pode sumir por isso.
    [Fact]
    public void Sem_capa_o_cartão_ainda_sai()
    {
        var png = CartãoDaFicha.Png(Ficha(), "Sem capa nenhuma.", capa: null);

        Assert.NotNull(png);

        using var bitmap = SKBitmap.Decode(png);

        Assert.Equal(CartãoDaFicha.Largura, bitmap.Width);
    }

    // Bytes que não são imagem chegam quando a fonte devolve erro no lugar da capa.
    [Fact]
    public void Capa_corrompida_não_derruba_o_cartão()
    {
        var png = CartãoDaFicha.Png(Ficha(), "Capa quebrada.", [1, 2, 3, 4, 5]);

        Assert.NotNull(png);
    }

    [Fact]
    public void A_sinopse_vira_uma_linha_só()
    {
        Assert.Equal(
            "Primeira linha. Segunda linha.",
            CartãoDaFicha.Limpa("Primeira linha.\n\n   Segunda    linha.\n"));
    }

    [Fact]
    public void Os_dados_saem_na_ordem_nota_episódios_ano()
    {
        Assert.Equal(["9.10", "28 EPS", "2023"], CartãoDaFicha.Dados(Ficha()));
    }

    // Obra sem nota e sem data existe; a linha só mostra o que há.
    [Fact]
    public void Dado_que_não_existe_não_vira_campo_vazio()
    {
        var obra = new Obra { Tipo = TipoDeObra.Manga, Id = 1, Título = "Nada" };

        Assert.Empty(CartãoDaFicha.Dados(obra));
    }

    // Título de duas linhas empurra tudo: quem cede é a sinopse, pra gênero e elenco sobreviverem.
    [Fact]
    public void Título_que_ocupa_duas_linhas_encurta_a_sinopse()
    {
        var umaLinha = CartãoDaFicha.LinhasQueCabem(258);
        var duasLinhas = CartãoDaFicha.LinhasQueCabem(308);

        Assert.Equal(CartãoDaFicha.LinhasDeSinopse, umaLinha);
        Assert.True(duasLinhas < umaLinha, $"{duasLinhas} não é menos que {umaLinha}");
        Assert.True(duasLinhas >= 1, "sempre sobra pelo menos uma linha");
    }

    // Mesmo espremido, o cartão não pede número negativo de linhas.
    [Fact]
    public void Sem_espaço_ainda_cabe_uma_linha()
    {
        Assert.Equal(1, CartãoDaFicha.LinhasQueCabem(CartãoDaFicha.Altura));
    }

    [Fact]
    public void A_quebra_respeita_o_teto_de_linhas()
    {
        using var fonte = Fonte(21);
        var texto = string.Join(" ", Enumerable.Repeat("palavra", 400));

        var linhas = CartãoDaFicha.Quebra(texto, fonte, largura: 500, máximo: 4);

        Assert.Equal(4, linhas.Count);
        Assert.All(linhas, l => Assert.True(fonte.MeasureText(l) <= 500, $"vazou: {l}"));
    }

    // Sobrou texto: a última linha avisa, senão parece que a sinopse acabou ali.
    [Fact]
    public void Texto_cortado_termina_em_reticências()
    {
        using var fonte = Fonte(21);
        var texto = string.Join(" ", Enumerable.Repeat("palavra", 400));

        var linhas = CartãoDaFicha.Quebra(texto, fonte, largura: 500, máximo: 4);

        Assert.EndsWith("…", linhas[^1]);
    }

    [Fact]
    public void Texto_que_cabe_inteiro_não_leva_reticências()
    {
        using var fonte = Fonte(21);

        var linhas = CartãoDaFicha.Quebra("duas palavras", fonte, largura: 500, máximo: 4);

        Assert.Equal(["duas palavras"], linhas);
    }

    // Japonês vem sem espaço: sem corte por caractere, a linha inteira vazaria da caixa.
    [Fact]
    public void Palavra_sem_espaço_é_cortada_no_caractere()
    {
        using var fonte = Fonte(21);
        var japonês = new string('あ', 200);

        var linhas = CartãoDaFicha.Quebra(japonês, fonte, largura: 300, máximo: 3);

        Assert.NotEmpty(linhas);
        Assert.All(linhas, l => Assert.True(fonte.MeasureText(l) <= 300, $"vazou: {l}"));
    }

    [Fact]
    public void Corta_encurta_até_caber()
    {
        using var fonte = Fonte(21);
        var longo = new string('W', 300);

        var curto = CartãoDaFicha.Corta(longo, fonte, largura: 200);

        Assert.True(fonte.MeasureText(curto) <= 200);
        Assert.EndsWith("…", curto);
    }

    // A fonte do corpo é a que tem japonês; é com ela que a medida tem que bater.
    private static SKFont Fonte(float tamanho)
    {
        var png = CartãoDaFicha.Png(Ficha(), "aquece as fontes", capa: null);

        Assert.NotNull(png);

        return new SKFont(SKTypeface.Default, tamanho);
    }
}

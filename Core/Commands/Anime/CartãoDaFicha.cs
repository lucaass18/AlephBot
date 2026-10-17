using System.Collections.Concurrent;
using System.Reflection;

using AlephBot.Threnodian.Rede;

using AlephBot.Threnodian.MyAnimeList;

using NetCord.Rest;

using SkiaSharp;

namespace AlephBot.Core.Commands.Anime;

// A ficha do /ma desenhada. Nome de campo de embed é texto puro: nenhuma figura entra ali, nem
// por anexo nem por link. Então a ficha inteira vira uma imagem só, e o embed fica de moldura.
internal static class CartãoDaFicha
{
    // O dobro do que o Discord mostra (~400px de largura): reduzido pela metade, o texto sai nítido.
    internal const int Largura = 880;
    internal const int Altura = 480;

    private const int Margem = 36;

    // A capa na proporção em que as fontes entregam, 2:3.
    private const int CapaLargura = 240;
    private const int CapaAltura = 360;
    private const int CapaTopo = 72;

    // Onde a coluna da direita começa e quanto ela tem.
    private const int ColunaX = Margem + CapaLargura + 32;
    private const int ColunaLargura = Largura - ColunaX - Margem;

    // Quanto o chanfro corta de cada canto. É o que dá a cara de HUD.
    private const int Chanfro = 22;

    // Quantas linhas de sinopse cabem antes de a capa acabar.
    internal const int LinhasDeSinopse = 4;

    private static readonly SKColor Fundo = new(0x0B, 0x12, 0x18);
    private static readonly SKColor FundoClaro = new(0x11, 0x22, 0x2E);
    private static readonly SKColor Ciano = new(0x4F, 0xD6, 0xE8);
    private static readonly SKColor Texto = new(0xE6, 0xEE, 0xF2);
    private static readonly SKColor TextoFraco = new(0x8F, 0xA3, 0xAE);
    private static readonly SKColor Linha = new(0x1E, 0x35, 0x42);

    // A capa chega grande e é reduzida: sem filtro bom ela sai serrilhada.
    private static readonly SKSamplingOptions Suave = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private static readonly Lazy<SKTypeface> Condensada =
        new(() => Carrega("SairaCondensed-Bold.ttf"));

    // Cobre latim e japonês: título nativo sem ela vira quadradinho.
    private static readonly Lazy<SKTypeface> Corpo =
        new(() => Carrega("NotoSansJP.ttf"));

    private static SKTypeface Carrega(string arquivo)
    {
        var assembly = typeof(CartãoDaFicha).GetTypeInfo().Assembly;
        var nome = Array.Find(
            assembly.GetManifestResourceNames(),
            r => r.EndsWith(arquivo, StringComparison.Ordinal));

        if (nome is null)
            throw new InvalidOperationException($"A fonte {arquivo} não está embutida no assembly.");

        using var fluxo = assembly.GetManifestResourceStream(nome)!;

        return SKTypeface.FromStream(fluxo)
               ?? throw new InvalidOperationException($"A fonte {arquivo} não foi lida pelo Skia.");
    }

    // A condensada não tem japonês. Título nativo vai na Noto, que tem os dois.
    private static SKTypeface ParaTítulo(string texto) =>
        texto.All(c => c < 0x0250) ? Condensada.Value : Corpo.Value;

    // O nome do arquivo que o embed referencia por attachment://.
    internal const string Arquivo = "ficha.png";

    // Curto de propósito: capa que demora atrasa a resposta, e sem ela o cartão ainda sai.
    private static readonly HttpClient Http = ClienteHttp.Novo(TimeSpan.FromSeconds(4));

    // Quantos cartões prontos eu guardo antes de recomeçar.
    private const int MaxGuardados = 48;

    private static readonly ConcurrentDictionary<string, byte[]> Guardados = new();

    // O cartão pronto pra subir junto da mensagem, ou null. Nunca levanta.
    internal static async Task<AttachmentProperties?> AnexoAsync(
        Obra obra, string? sinopse, CancellationToken cancellationToken)
    {
        var chave = $"{obra.Fonte}|{obra.Tipo}|{obra.Id}|{(sinopse ?? "").Length}";

        if (!Guardados.TryGetValue(chave, out var png))
        {
            png = Png(obra, sinopse, await CapaAsync(obra, cancellationToken));

            if (png is null)
                return null;

            // teto simples: cheio, recomeça. Cartão é barato de refazer e isto não tem dono
            if (Guardados.Count >= MaxGuardados)
                Guardados.Clear();

            Guardados[chave] = png;
        }

        return new AttachmentProperties(Arquivo, new MemoryStream(png, writable: false));
    }

    private static async Task<byte[]?> CapaAsync(Obra obra, CancellationToken cancellationToken)
    {
        if (obra.Imagem is not { Length: > 0 } endereço)
            return null;

        try
        {
            return await Http.GetByteArrayAsync(endereço, cancellationToken);
        }
        catch (Exception)
        {
            // capa é o enfeite do enfeite: sem ela o cartão sai com a moldura vazia
            return null;
        }
    }

    // O PNG da ficha, ou null quando algo faltou. Nunca levanta: quem chama volta pro embed.
    internal static byte[]? Png(Obra obra, string? sinopse, byte[]? capa)
    {
        try
        {
            return Desenha(obra, sinopse, capa);
        }
        catch (Exception)
        {
            // cartão é enfeite; a resposta não pode morrer por causa dele
            return null;
        }
    }

    private static byte[] Desenha(Obra obra, string? sinopse, byte[]? capa)
    {
        using var superfície = SKSurface.Create(new SKImageInfo(Largura, Altura));
        var tela = superfície.Canvas;

        DesenhaFundo(tela);
        DesenhaCapa(tela, capa);
        DesenhaTexto(tela, obra, sinopse);

        using var imagem = superfície.Snapshot();
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);

        return dados.ToArray();
    }

    // O fundo com os cantos chanfrados e o brilho frio vindo de cima.
    private static void DesenhaFundo(SKCanvas tela)
    {
        tela.Clear(SKColors.Transparent);

        using var moldura = Caminho(fechado: true,
            (Chanfro, 0), (Largura, 0), (Largura, Altura - Chanfro),
            (Largura - Chanfro, Altura), (0, Altura), (0, Chanfro));

        using var gradiente = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(Largura, Altura),
            [FundoClaro, Fundo],
            [0f, 0.7f],
            SKShaderTileMode.Clamp);

        using var pintura = new SKPaint { Shader = gradiente, IsAntialias = true };
        tela.DrawPath(moldura, pintura);

        // o fio ciano no chanfro de cima: é ele que dá o ar de interface de jogo
        using var fio = new SKPaint
        {
            Color = Ciano,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        };

        using var corte = Caminho(fechado: false, (0, Chanfro), (Chanfro, 0));
        tela.DrawPath(corte, fio);

        using var corteBaixo = Caminho(fechado: false,
            (Largura - Chanfro, Altura), (Largura, Altura - Chanfro));
        tela.DrawPath(corteBaixo, fio);
    }

    // O SKPath direto está obsoleto; o construtor é o caminho novo.
    private static SKPath Caminho(bool fechado, params (float X, float Y)[] pontos)
    {
        using var construtor = new SKPathBuilder();

        construtor.MoveTo(pontos[0].X, pontos[0].Y);

        foreach (var ponto in pontos.AsSpan(1))
            construtor.LineTo(ponto.X, ponto.Y);

        if (fechado)
            construtor.Close();

        return construtor.Detach();
    }

    private static void DesenhaCapa(SKCanvas tela, byte[]? capa)
    {
        var caixa = new SKRect(Margem, CapaTopo, Margem + CapaLargura, CapaTopo + CapaAltura);

        using var fundo = new SKPaint { Color = Linha, IsAntialias = true };
        tela.DrawRect(caixa, fundo);

        if (capa is { Length: > 0 } bytes)
        {
            // bytes que não são imagem chegam: fonte fora devolve página de erro no lugar da capa.
            // Isso tira a capa do cartão, e não o cartão da resposta — daí o catch ser só aqui.
            using var bitmap = Decodifica(bytes);

            if (bitmap is not null)
            {
                // cobre a caixa inteira sem distorcer: o que sobra de um lado é cortado
                var escala = Math.Max(caixa.Width / bitmap.Width, caixa.Height / bitmap.Height);
                var larguraFinal = bitmap.Width * escala;
                var alturaFinal = bitmap.Height * escala;

                var destino = new SKRect(
                    caixa.MidX - larguraFinal / 2,
                    caixa.MidY - alturaFinal / 2,
                    caixa.MidX + larguraFinal / 2,
                    caixa.MidY + alturaFinal / 2);

                tela.Save();
                tela.ClipRect(caixa);
                tela.DrawBitmap(bitmap, destino, Suave);
                tela.Restore();
            }
        }

        using var borda = new SKPaint
        {
            Color = Ciano,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        };
        tela.DrawRect(caixa, borda);
    }

    private static SKBitmap? Decodifica(byte[] bytes)
    {
        try
        {
            return SKBitmap.Decode(bytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void DesenhaTexto(SKCanvas tela, Obra obra, string? sinopse)
    {
        using var tinta = new SKPaint { IsAntialias = true };

        // ---- título ----
        var título = (obra.Título ?? "").ToUpperInvariant();
        using var fonteTítulo = new SKFont(ParaTítulo(título), 46) { Edging = SKFontEdging.SubpixelAntialias };

        var linhasTítulo = Quebra(título, fonteTítulo, ColunaLargura, 2);
        var y = (float)CapaTopo + 34;

        tinta.Color = Texto;
        foreach (var linha in linhasTítulo)
        {
            tela.DrawText(linha, ColunaX, y, SKTextAlign.Left, fonteTítulo, tinta);
            y += 50;
        }

        // ---- título nativo ----
        if (obra.TítuloEmJaponês is { Length: > 0 } japonês)
        {
            using var fonteNativa = new SKFont(Corpo.Value, 22) { Edging = SKFontEdging.SubpixelAntialias };
            tinta.Color = TextoFraco;
            tela.DrawText(Corta(japonês, fonteNativa, ColunaLargura), ColunaX, y + 4, SKTextAlign.Left, fonteNativa, tinta);
            y += 34;
        }

        y += 14;

        // ---- a linha dos números ----
        using var fonteDado = new SKFont(Condensada.Value, 28) { Edging = SKFontEdging.SubpixelAntialias };
        var x = (float)ColunaX;

        foreach (var dado in Dados(obra))
        {
            if (x > ColunaX && x + fonteDado.MeasureText(dado) + 28 > Largura - Margem)
                break;

            // o marcador vai desenhado, e não escrito: a condensada não tem o glifo do triângulo
            tinta.Color = Ciano;
            using (var seta = Caminho(fechado: true,
                       (x, y - 17), (x + 11, y - 10), (x, y - 3)))
            {
                tela.DrawPath(seta, tinta);
            }

            tinta.Color = Texto;
            tela.DrawText(dado, x + 22, y, SKTextAlign.Left, fonteDado, tinta);

            x += 22 + fonteDado.MeasureText(dado) + 30;
        }

        y += 20;

        // ---- fio divisor ----
        using var fio = new SKPaint { Color = Linha, StrokeWidth = 2, IsAntialias = true };
        tela.DrawLine(ColunaX, y, Largura - Margem, y, fio);

        y += 34;

        // ---- sinopse ----
        if (Limpa(sinopse) is { Length: > 0 } corpo)
        {
            using var fonteSinopse = new SKFont(Corpo.Value, 21) { Edging = SKFontEdging.SubpixelAntialias };
            tinta.Color = TextoFraco;

            foreach (var linha in Quebra(corpo, fonteSinopse, ColunaLargura, LinhasDeSinopse))
            {
                tela.DrawText(linha, ColunaX, y, SKTextAlign.Left, fonteSinopse, tinta);
                y += 31;
            }
        }

        var depoisDosGêneros = DesenhaGêneros(tela, obra, tinta, y + 16);

        DesenhaElenco(tela, obra, tinta, depoisDosGêneros);
    }

    // O elenco numa linha só, fechando o cartão. O embed de campos mostrava isto e sumiria sem.
    private static void DesenhaElenco(SKCanvas tela, Obra obra, SKPaint tinta, float y)
    {
        if (Elenco(obra) is not { Length: > 0 } elenco || y > Altura - Margem)
            return;

        using var fonte = new SKFont(Corpo.Value, 18) { Edging = SKFontEdging.SubpixelAntialias };

        tinta.Color = TextoFraco;
        tela.DrawText(Corta(elenco, fonte, ColunaLargura), ColunaX, y, SKTextAlign.Left, fonte, tinta);
    }

    // Os nomes do elenco em sequência, ou null quando a obra não tem elenco cadastrado.
    internal static string? Elenco(Obra obra)
    {
        if (obra.Personagens.Count == 0)
            return null;

        return string.Join(" · ", obra.Personagens.Select(quem => quem.Nome));
    }

    // As cápsulas de gênero seguem a coluna da direita; embaixo da capa sobrava buraco.
    // Devolve a altura em que o próximo bloco pode começar.
    private static float DesenhaGêneros(SKCanvas tela, Obra obra, SKPaint tinta, float y)
    {
        if (obra.Gêneros.Count == 0 || y > Altura - Margem)
            return y;

        using var fonte = new SKFont(Condensada.Value, 22) { Edging = SKFontEdging.SubpixelAntialias };
        var x = (float)ColunaX;

        using var borda = new SKPaint
        {
            Color = Linha,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        };

        foreach (var gênero in obra.Gêneros)
        {
            var texto = gênero.ToUpperInvariant();
            var largura = fonte.MeasureText(texto) + 26;

            if (x + largura > Largura - Margem)
                break;

            tela.DrawRoundRect(new SKRect(x, y - 22, x + largura, y + 8), 3, 3, borda);

            tinta.Color = Ciano;
            tela.DrawText(texto, x + 13, y, SKTextAlign.Left, fonte, tinta);

            x += largura + 10;
        }

        return y + 30;
    }

    // Os números que cabem numa linha só: nota, formato e período.
    internal static IReadOnlyList<string> Dados(Obra obra)
    {
        var dados = new List<string>();

        if (obra.Nota is { } nota)
            dados.Add(nota.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

        if (obra.Episódios is { } episódios)
            dados.Add($"{episódios} EPS");
        else if (obra.Capítulos is { } capítulos)
            dados.Add($"{capítulos} CAPS");

        if (obra.Início.Ano is { } ano)
            dados.Add(ano.ToString());

        return dados;
    }

    // A sinopse numa linha só: a quebra quem faz é o desenho, não o texto que chegou.
    internal static string Limpa(string? sinopse) =>
        string.Join(' ', (sinopse ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Quebra o texto na largura dada, no máximo em tantas linhas; a última ganha reticências.
    internal static IReadOnlyList<string> Quebra(string texto, SKFont fonte, float largura, int máximo)
    {
        var linhas = new List<string>();

        if (string.IsNullOrWhiteSpace(texto) || máximo <= 0)
            return linhas;

        var palavras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var atual = "";

        foreach (var palavra in palavras)
        {
            var tentativa = atual.Length == 0 ? palavra : atual + " " + palavra;

            if (fonte.MeasureText(tentativa) <= largura)
            {
                atual = tentativa;
                continue;
            }

            if (atual.Length > 0)
                linhas.Add(atual);

            // palavra sozinha maior que a linha (japonês vem sem espaço): corta no caractere
            if (fonte.MeasureText(palavra) > largura)
            {
                var resto = palavra;

                while (fonte.MeasureText(resto) > largura && linhas.Count < máximo)
                {
                    var cabe = QuantosCabem(resto, fonte, largura);

                    if (cabe == 0)
                        break;

                    linhas.Add(resto[..cabe]);
                    resto = resto[cabe..];
                }

                atual = resto;
            }
            else
            {
                atual = palavra;
            }

            if (linhas.Count >= máximo)
                break;
        }

        if (linhas.Count < máximo && atual.Length > 0)
            linhas.Add(atual);

        if (linhas.Count > máximo)
            linhas.RemoveRange(máximo, linhas.Count - máximo);

        // sobrou texto: a última linha avisa que continua
        var coube = string.Join(' ', linhas).Length;

        if (linhas.Count == máximo && coube < texto.Length)
            linhas[^1] = Corta(linhas[^1] + "…", fonte, largura);

        return linhas;
    }

    private static int QuantosCabem(string texto, SKFont fonte, float largura)
    {
        var n = 0;

        while (n < texto.Length && fonte.MeasureText(texto[..(n + 1)]) <= largura)
            n++;

        return n;
    }

    // Encurta até caber, com reticências no fim.
    internal static string Corta(string texto, SKFont fonte, float largura)
    {
        if (fonte.MeasureText(texto) <= largura)
            return texto;

        var n = QuantosCabem(texto, fonte, largura - fonte.MeasureText("…"));

        return n <= 0 ? "" : texto[..n].TrimEnd() + "…";
    }
}

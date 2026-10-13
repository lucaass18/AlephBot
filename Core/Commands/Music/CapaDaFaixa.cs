using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;

using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// A capa da faixa como anexo da mensagem, e não como imagem do embed. O Discord limita a
/// largura do embed, e a imagem dentro dele nunca passa disso; anexo ocupa a largura do
/// canal e abre em tela cheia num clique. O preço é baixar a arte e subir de novo a cada
/// anúncio — por isso só o "tocando agora" usa, e a ficha da fila segue com a imagem do embed.
/// </summary>
internal static class CapaDaFaixa
{
    /// <summary>
    /// Capa maior que isto eu deixo pra lá. O limite do Discord é bem mais alto; este é só
    /// pra não segurar o anúncio enquanto baixo a arte em tamanho de pôster.
    /// </summary>
    private const int MaxBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Curto de propósito: a mensagem é o que importa, e capa que demora é capa que atrasa
    /// o canal inteiro. O que não chegar a tempo simplesmente não vai.
    /// </summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>
    /// A capa pronta pra virar anexo, ou null quando a faixa não tem arte, a arte não veio
    /// ou veio grande demais. Nunca estoura: anúncio sem capa é melhor que anúncio nenhum.
    /// </summary>
    internal static async Task<AttachmentProperties?> BaixarAsync(
        LavalinkTrack faixa, ILogger? logger, CancellationToken cancellationToken)
    {
        if (faixa.ArtworkUri is not { } capa)
            return null;

        try
        {
            using var resposta = await Http.GetAsync(
                capa, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!resposta.IsSuccessStatusCode || resposta.Content.Headers.ContentLength > MaxBytes)
                return null;

            var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);

            if (bytes.Length > MaxBytes)
                return null;

            return new AttachmentProperties(Nome(capa), new MemoryStream(bytes));
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException
                                     && !cancellationToken.IsCancellationRequested)
        {
            logger?.LogDebug(erro, "Não baixei a capa de {Faixa}", faixa.Title);
            return null;
        }
    }

    /// <summary>
    /// O nome do arquivo decide se o Discord mostra a imagem ou oferece um download: sem
    /// extensão conhecida, vira anexo mudo. Na dúvida, .jpg — é o que o YouTube serve.
    /// </summary>
    private static string Nome(Uri capa)
    {
        var extensão = Path.GetExtension(capa.AbsolutePath).ToLowerInvariant();

        return extensão is ".png" or ".gif" or ".webp" ? $"capa{extensão}" : "capa.jpg";
    }
}

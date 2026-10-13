using System.Collections.Concurrent;

using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;

using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// A capa da faixa como arquivo meu, e não como link pro YouTube. Dando o link, o Discord
/// só vai buscar a imagem quando alguém abre o canal, e o card fica segundos com o buraco
/// no lugar dela; subindo o arquivo junto da mensagem, ela já está no CDN dele quando o
/// card aparece. O preço é baixar e subir a arte — e é por isso que eu adianto a da próxima
/// faixa enquanto a de agora toca.
/// </summary>
internal static class CapaDaFaixa
{
    /// <summary>
    /// Capa maior que isto eu deixo pra lá. O limite do Discord é bem mais alto; este é só
    /// pra não segurar o anúncio enquanto baixo a arte em tamanho de pôster.
    /// </summary>
    private const int MaxBytes = 4 * 1024 * 1024;

    /// <summary>Quantas capas eu guardo antes de recomeçar: a de agora, a próxima e folga.</summary>
    private const int MaxGuardadas = 16;

    /// <summary>
    /// Curto de propósito: a mensagem é o que importa, e capa que demora é capa que atrasa
    /// o canal inteiro. O que não chegar a tempo vai como link, do jeito antigo.
    /// </summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    private static readonly ConcurrentDictionary<string, byte[]> Guardadas = new();

    /// <summary>
    /// Começa a baixar a capa de uma faixa que ainda vai tocar. Não espera nem reclama: se
    /// der certo, o anúncio dela sai sem pagar o download; se não, paga na hora.
    /// </summary>
    internal static void Adiantar(LavalinkTrack? faixa, ILogger? logger)
    {
        if (faixa?.ArtworkUri is { } capa && !Guardadas.ContainsKey(capa.AbsoluteUri))
            _ = BytesAsync(capa, logger, CancellationToken.None);
    }

    /// <summary>
    /// A capa pronta pra subir junto da mensagem, ou null quando a faixa não tem arte, ela
    /// não veio ou veio grande demais. Nunca estoura: card com link da capa é melhor que
    /// card nenhum.
    /// </summary>
    internal static async Task<AttachmentProperties?> AnexoAsync(
        LavalinkTrack faixa, ILogger? logger, CancellationToken cancellationToken)
    {
        if (faixa.ArtworkUri is not { } capa)
            return null;

        var bytes = await BytesAsync(capa, logger, cancellationToken);

        return bytes is null ? null : new AttachmentProperties(Nome(capa), new MemoryStream(bytes));
    }

    private static async Task<byte[]?> BytesAsync(Uri capa, ILogger? logger, CancellationToken cancellationToken)
    {
        if (Guardadas.TryGetValue(capa.AbsoluteUri, out var guardada))
            return guardada;

        try
        {
            using var resposta = await Http.GetAsync(
                capa, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!resposta.IsSuccessStatusCode || resposta.Content.Headers.ContentLength > MaxBytes)
                return null;

            var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);

            if (bytes.Length > MaxBytes)
                return null;

            // o teto evita a memória crescer sozinha num servidor que toca o dia inteiro
            if (Guardadas.Count >= MaxGuardadas)
                Guardadas.Clear();

            Guardadas[capa.AbsoluteUri] = bytes;

            return bytes;
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException
                                     && !cancellationToken.IsCancellationRequested)
        {
            logger?.LogDebug(erro, "Não baixei a capa de {Capa}", capa);
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

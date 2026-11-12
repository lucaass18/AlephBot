using System.Collections.Concurrent;

using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;

using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

// A capa como arquivo meu, não como link: pelo link o card fica segundos com o buraco no lugar.
internal static class CapaDaFaixa
{
    // Capa maior que isto eu deixo pra lá, pra não segurar o anúncio baixando um pôster.
    private const int MaxBytes = 4 * 1024 * 1024;

    // Quantas capas eu guardo antes de recomeçar: a de agora, a próxima e folga.
    private const int MaxGuardadas = 16;

    // Curto de propósito: capa que demora atrasa o canal. O que não chega vai como link.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    private static readonly ConcurrentDictionary<string, byte[]> Guardadas = new();

    // Começa a baixar a capa de faixa que ainda vai tocar; não espera nem reclama.
    internal static void Adiantar(LavalinkTrack? faixa, ILogger? logger)
    {
        if (faixa?.ArtworkUri is { } capa && !Guardadas.ContainsKey(capa.AbsoluteUri))
            _ = BytesAsync(capa, logger, CancellationToken.None);
    }

    // A capa pronta pra subir, ou null. Nunca estoura: card com link é melhor que card nenhum.
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

    // O nome decide se o Discord mostra a imagem ou um download mudo. Na dúvida, .jpg.
    private static string Nome(Uri capa)
    {
        var extensão = Path.GetExtension(capa.AbsolutePath).ToLowerInvariant();

        return extensão is ".png" or ".gif" or ".webp" ? $"capa{extensão}" : "capa.jpg";
    }
}

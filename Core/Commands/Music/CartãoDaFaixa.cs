using System.Text;

using AlephBot.Core.Personality;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// O card pronto pra enviar: o container e, quando deu pra baixar a capa a tempo, o arquivo
/// dela. O Discord exige que o arquivo vá na mesma mensagem que o referencia.
/// </summary>
internal readonly record struct Cartão(ComponentContainerProperties Container, AttachmentProperties? Anexo);

/// <summary>
/// O "tocando agora" no formato novo de mensagem do Discord, e não como embed. O motivo é
/// um só: a imagem de um embed nunca passa da largura dele, e aqui a capa entra numa galeria
/// de mídia, que ocupa a largura do container inteiro. O resto é o mesmo conteúdo de antes —
/// a barra roxa virou a cor do container, e os campos viraram uma linha de texto miúdo, que
/// é o que este formato tem no lugar de colunas.
/// </summary>
internal static class CartãoDaFaixa
{
    /// <summary>A faixa que começou a tocar, com a próxima quando tem fila.</summary>
    internal static async Task<Cartão> TocandoAsync(
        LavalinkTrack faixa,
        string? quemPediu,
        LavalinkTrack? próxima,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var texto = new StringBuilder(Denia.MúsicaComeçou());

        if (próxima is not null)
            texto.Append('\n').Append(Denia.MúsicaCampoASeguir).Append(": ").Append(Music.Link(próxima));

        var cartão = await MontarAsync(
            Denia.MúsicaTítuloTocando,
            faixa,
            texto.ToString(),
            Music.DuraçãoDe(faixa),
            quemPediu,
            logger,
            cancellationToken);

        // a próxima começa quando esta acabar: a capa dela já pode ir sendo baixada
        CapaDaFaixa.Adiantar(próxima, logger);

        return cartão;
    }

    /// <summary>O /nowplaying: a mesma faixa, com a barra de progresso no lugar da frase.</summary>
    internal static Task<Cartão> ProgressoAsync(
        AlephPlayer player, LavalinkTrack faixa, string progresso, CancellationToken cancellationToken = default)
    {
        var rótulo = player.IsPaused ? Denia.MúsicaTítuloPausada : Denia.MúsicaTítuloTocando;

        if (player.RepeatMode is not TrackRepeatMode.None)
            rótulo = $"{rótulo} · {Music.Repetição(player.RepeatMode)}";

        return MontarAsync(
            rótulo,
            faixa,
            progresso,
            Music.DuraçãoDe(faixa),
            Music.QuemPediu(player.CurrentItem),
            logger: null,
            cancellationToken);
    }

    private static async Task<Cartão> MontarAsync(
        string rótulo,
        LavalinkTrack faixa,
        string corpo,
        string duração,
        string? quemPediu,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var componentes = new List<IComponentContainerComponentProperties>
        {
            // "-#" é o texto miúdo do Discord: faz as vezes do rótulo pequeno do embed
            new TextDisplayProperties($"-# {rótulo}"),
            new TextDisplayProperties($"### {Music.Link(faixa)}"),
            new TextDisplayProperties(corpo),
        };

        var anexo = await CapaDaFaixa.AnexoAsync(faixa, logger, cancellationToken);

        // o arquivo que sobe junto é referenciado por nome; sem ele, resta o link, e aí a
        // capa só aparece quando o Discord for buscar ela no YouTube
        var capa = anexo is not null ? $"attachment://{anexo.FileName}" : faixa.ArtworkUri?.ToString();

        if (capa is not null)
        {
            componentes.Add(new MediaGalleryProperties(
                [new MediaGalleryItemProperties(new ComponentMediaProperties(capa))]));
        }

        componentes.Add(new TextDisplayProperties($"-# {Rodapé(faixa, duração, quemPediu)}"));

        var container = new ComponentContainerProperties(componentes) { AccentColor = new Color(Music.Roxo) };

        return new Cartão(container, anexo);
    }

    /// <summary>
    /// A linha miúda do fim: o que no embed eram os campos e o rodapé. Vai tudo junto porque
    /// este formato não tem coluna — e três linhas curtas pesariam mais que uma só.
    /// </summary>
    private static string Rodapé(LavalinkTrack faixa, string duração, string? quemPediu)
    {
        var partes = new List<string>
        {
            $"{Denia.MúsicaCampoArtista} {Music.Artista(faixa)}",
            $"{Denia.MúsicaCampoDuração} `{duração}`",
        };

        if (!string.IsNullOrWhiteSpace(faixa.SourceName))
            partes.Add(faixa.SourceName);

        if (!string.IsNullOrWhiteSpace(quemPediu))
            partes.Add($"pedido por {quemPediu}");

        return string.Join(" · ", partes);
    }
}

using System.Text;

using AlephBot.Core.Personality;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

// O card pronto: o container e, quando a capa chegou a tempo, o arquivo dela na mesma mensagem.
internal readonly record struct Cartão(ComponentContainerProperties Container, AttachmentProperties? Anexo);

// O "tocando agora" no formato novo: a capa entra em galeria e ocupa a largura inteira.
internal static class CartãoDaFaixa
{
    // A faixa que começou a tocar, com a próxima quando tem fila.
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

    // O /nowplaying: a mesma faixa, com a barra de progresso no lugar da frase.
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

        // o arquivo que sobe junto é referenciado por nome; sem ele resta o link, que demora
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

    // A linha miúda do fim: o que eram campos e rodapé. Este formato não tem coluna.
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

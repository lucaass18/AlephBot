using System.Text;

using AlephBot.Core.Personality;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

using NetCord;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Music;

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
    internal static ComponentContainerProperties Tocando(
        LavalinkTrack faixa, string? quemPediu, LavalinkTrack? próxima)
    {
        var texto = new StringBuilder(Denia.MúsicaComeçou());

        if (próxima is not null)
            texto.Append('\n').Append(Denia.MúsicaCampoASeguir).Append(": ").Append(Music.Link(próxima));

        return Montar(
            Denia.MúsicaTítuloTocando,
            faixa,
            texto.ToString(),
            Music.DuraçãoDe(faixa),
            quemPediu);
    }

    /// <summary>O /nowplaying: a mesma faixa, com a barra de progresso no lugar da frase.</summary>
    internal static ComponentContainerProperties Progresso(AlephPlayer player, LavalinkTrack faixa, string progresso)
    {
        var rótulo = player.IsPaused ? Denia.MúsicaTítuloPausada : Denia.MúsicaTítuloTocando;

        if (player.RepeatMode is not TrackRepeatMode.None)
            rótulo = $"{rótulo} · {Music.Repetição(player.RepeatMode)}";

        return Montar(
            rótulo,
            faixa,
            progresso,
            Music.DuraçãoDe(faixa),
            Music.QuemPediu(player.CurrentItem));
    }

    private static ComponentContainerProperties Montar(
        string rótulo, LavalinkTrack faixa, string corpo, string duração, string? quemPediu)
    {
        var componentes = new List<IComponentContainerComponentProperties>
        {
            // "-#" é o texto miúdo do Discord: faz as vezes do rótulo pequeno do embed
            new TextDisplayProperties($"-# {rótulo}"),
            new TextDisplayProperties($"### {Music.Link(faixa)}"),
            new TextDisplayProperties(corpo),
        };

        if (faixa.ArtworkUri is { } capa)
        {
            componentes.Add(new MediaGalleryProperties(
                [new MediaGalleryItemProperties(new ComponentMediaProperties(capa.ToString()))]));
        }

        componentes.Add(new TextDisplayProperties($"-# {Rodapé(faixa, duração, quemPediu)}"));

        return new ComponentContainerProperties(componentes) { AccentColor = new Color(Music.Roxo) };
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

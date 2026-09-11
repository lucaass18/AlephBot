using System.Text.Json;

using AlephBot.Core.Personality;
using AlephBot.Threnodian.Youtube;

using Lavalink4NET;

using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// /playlist add — acha uma playlist do YouTube pelo nome (ou pelo link) e põe ela inteira
/// na fila. O /play já aceita link de playlist; a diferença está no nome: lá "nightcore
/// rock" vira o primeiro vídeo, aqui vira a primeira playlist.
/// </summary>
[SlashCommand("playlist", "Playlists do YouTube.")]
public sealed class PlaylistCommand : MusicSlashModule
{
    private readonly YoutubeSearch _youtube;

    public PlaylistCommand(IAudioService áudio, YoutubeSearch youtube) : base(áudio)
    {
        _youtube = youtube;
    }

    public override string Name => "playlist";
    public override string Description => "Põe uma playlist inteira do YouTube na fila.";
    public override string? Usage => Playlist.Uso;

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SubSlashCommand("add", "Procura uma playlist do YouTube e põe ela inteira na fila.")]
    public async Task AddAsync(
        [SlashCommandParameter(Name = "busca", Description = "Nome da playlist ou o link dela.")] string busca)
    {
        // entrar no canal, perguntar ao YouTube e carregar a playlist passa fácil dos
        // 3 segundos que o Discord dá; peço tempo
        await DeferAsync();

        var (player, erro) = await Music.PlayerAsync(Áudio, Context, Context.Channel, conectar: true);

        if (erro is not null)
        {
            await EditarComErroAsync(erro);
            return;
        }

        var resposta = await Playlist.AdicionarAsync(Áudio, _youtube, player!, busca, Context.User.Username);

        // fora do ExecutarAsync da base por causa do defer; a foto continua sendo minha responsabilidade
        player!.Fotografar();

        if (resposta.Erro is { } falha)
            await EditarComErroAsync(falha);
        else
            await EditarRespostaAsync(resposta.Embed!);
    }
}

/// <summary>!playlist add — o mesmo do /playlist add, para quem prefere o prefixo.</summary>
public sealed class PlaylistTextCommand : MusicTextModule
{
    private readonly YoutubeSearch _youtube;

    public PlaylistTextCommand(IAudioService áudio, YoutubeSearch youtube) : base(áudio)
    {
        _youtube = youtube;
    }

    public override string Name => "playlist";
    public override string Description => "Põe uma playlist inteira do YouTube na fila.";
    public override string? Usage => Playlist.Uso;

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("playlist", "pl")]
    public Task PlaylistAsync(string ação, [CommandParameter(Remainder = true)] string busca)
    {
        // o comando de texto não tem subcomando de verdade: a primeira palavra faz o papel
        if (!Playlist.ÉAdd(ação))
            return ErrorAsync(Denia.MúsicaPlaylistAçãoDesconhecida(ação, Playlist.Uso));

        return ExecutarAsync(
            player => Playlist.AdicionarAsync(Áudio, _youtube, player, busca, Context.User.Username),
            conectar: true);
    }
}

internal static class Playlist
{
    internal const string Uso = "playlist add <nome ou link>";

    internal static bool ÉAdd(string ação) =>
        ação.Equals("add", StringComparison.OrdinalIgnoreCase)
        || ação.Equals("adicionar", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Link vai direto pro Lavalink; nome vira busca de playlist no YouTube antes. Nos dois
    /// casos o que chega tem que ser playlist — vídeo solto é recusado, e não tocado por
    /// engano, porque quem chamou este comando pediu uma lista.
    /// </summary>
    internal static async Task<Music.Resposta> AdicionarAsync(
        IAudioService áudio,
        YoutubeSearch youtube,
        AlephPlayer player,
        string busca,
        string quemPediu,
        CancellationToken cancellationToken = default)
    {
        busca = busca.Trim();

        // aqui a rádio automática (list=RD...) não é tirada de propósito: no /play ela é
        // um efeito colateral do link copiado; no /playlist add ela é a lista pedida
        var alvo = busca;

        if (!Music.ÉLink(busca))
        {
            string? id;

            try
            {
                id = await youtube.PlaylistAsync(busca, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return Music.Resposta.Falha(Denia.MúsicaPlaylistBuscaFalhou());
            }

            if (id is null)
                return Music.Resposta.Falha(Denia.MúsicaPlaylistNãoAchei(busca));

            alvo = YoutubeSearch.LinkDaPlaylist(id);
        }

        var (resultado, erro) = await Music.CarregarAsync(áudio, alvo, cancellationToken);

        if (erro is not null)
            return Music.Resposta.Falha(erro);

        if (!resultado.HasMatches)
            return Music.Resposta.Falha(Denia.MúsicaPlaylistNãoAchei(busca));

        if (!resultado.IsPlaylist)
            return Music.Resposta.Falha(Denia.MúsicaNãoÉPlaylist());

        return await Play.PlaylistAsync(player, resultado, busca, quemPediu, cancellationToken);
    }
}

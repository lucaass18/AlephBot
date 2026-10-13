using System.Text.Json;

using AlephBot.Core.Personality;
using AlephBot.Threnodian.Dados;
using AlephBot.Threnodian.Youtube;

using Lavalink4NET;

using NetCord;

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
    private readonly PlaylistsSalvas _salvas;

    public PlaylistCommand(IAudioService áudio, YoutubeSearch youtube, PlaylistsSalvas salvas) : base(áudio)
    {
        _youtube = youtube;
        _salvas = salvas;
    }

    public override string Name => "playlist";
    public override string Description => "Põe uma playlist do YouTube na fila, ou guarda a fila de agora.";
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

    // ---- as que ficam guardadas ----------------------------------------------

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SubSlashCommand("salvar", "Guarda a fila de agora com um nome, pra tocar outro dia.")]
    public async Task SalvarAsync(
        [SlashCommandParameter(Name = "nome", Description = "Como você vai chamar ela depois.")] string nome)
    {
        if (Context.Guild?.Id is not { } guildId)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        if (!PlaylistGuardada.NomeAceitável(nome))
        {
            await ErrorAsync(Denia.PlaylistNomeInválido(PlaylistSalva.NomeNoMáximo));
            return;
        }

        var (player, erro) = await Music.PlayerAsync(Áudio, Context, Context.Channel, conectar: false);

        if (erro is not null)
        {
            await ErrorAsync(erro);
            return;
        }

        var faixas = PlaylistGuardada.DaFila(player!);

        if (faixas.Count == 0)
        {
            await ErrorAsync(Denia.PlaylistFilaVazia());
            return;
        }

        var resultado = await _salvas.GuardaAsync(guildId, nome.Trim(), Context.User.Id, faixas);

        if (PlaylistGuardada.Recusa(resultado, nome) is { } recusa)
        {
            await ErrorAsync(recusa);
            return;
        }

        await RespondAsync(PlaylistGuardada.Guardou(nome.Trim(), faixas.Count));
    }

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SubSlashCommand("tocar", "Põe na fila uma playlist que foi guardada aqui.")]
    public async Task TocarAsync(
        [SlashCommandParameter(Name = "nome", Description = "O nome que você deu quando salvou.")] string nome)
    {
        if (Context.Guild?.Id is not { } guildId)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        // achar no banco, entrar no canal e enfileirar passa dos 3 segundos do Discord
        await DeferAsync();

        var playlist = await _salvas.AcharAsync(guildId, nome.Trim());

        if (playlist is null)
        {
            await EditarComErroAsync(Denia.PlaylistNãoAchei(nome.Trim()));
            return;
        }

        var (player, erro) = await Music.PlayerAsync(Áudio, Context, Context.Channel, conectar: true);

        if (erro is not null)
        {
            await EditarComErroAsync(erro);
            return;
        }

        var quantas = await PlaylistGuardada.ParaAFilaAsync(player!, playlist, Context.User.Username);

        player!.Fotografar();

        if (quantas == 0)
        {
            await EditarComErroAsync(Denia.PlaylistNadaTocável(playlist.Nome));
            return;
        }

        await EditarRespostaAsync(PlaylistGuardada.Enfileirou(playlist, quantas));

        // só agora: o anúncio da primeira faixa vem do player, e com ele antes da resposta
        // o canal mostrava o "tocando agora" em cima do aviso da playlist
        await PlaylistGuardada.ComeçarAsync(player!);
        player.Fotografar();
    }

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SubSlashCommand("lista", "Mostra as playlists guardadas neste servidor.")]
    public async Task ListaAsync()
    {
        if (Context.Guild?.Id is not { } guildId)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        if (!_salvas.Disponível)
        {
            await ErrorAsync(Denia.PlaylistSemBanco());
            return;
        }

        await RespondAsync(PlaylistGuardada.Lista(await _salvas.DoServidorAsync(guildId)));
    }

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageMessages)]
    [SubSlashCommand("apagar", "Apaga uma playlist guardada.")]
    public async Task ApagarAsync(
        [SlashCommandParameter(Name = "nome", Description = "Qual delas apagar.")] string nome)
    {
        if (Context.Guild?.Id is not { } guildId)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        if (!_salvas.Disponível)
        {
            await ErrorAsync(Denia.PlaylistSemBanco());
            return;
        }

        if (!await _salvas.ApagaAsync(guildId, nome.Trim()))
        {
            await ErrorAsync(Denia.PlaylistNãoAchei(nome.Trim()));
            return;
        }

        await SuccessAsync(Denia.PlaylistApagada(nome.Trim()));
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

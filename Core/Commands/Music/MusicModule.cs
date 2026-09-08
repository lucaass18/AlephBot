using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;

using Lavalink4NET;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// Base dos comandos de música em barra. Toda ação de música faz a mesma dança — pega o
/// player, desiste com uma frase se não deu, executa e responde — e é ela que mora aqui.
/// Classe abstrata não vira comando: nem o AddModules nem o CommandRegistry pegam ela.
/// </summary>
public abstract class MusicSlashModule : AlephSlashModule
{
    protected MusicSlashModule(IAudioService áudio)
    {
        Áudio = áudio;
    }

    protected IAudioService Áudio { get; }

    public override CommandCategory Category => CommandCategory.Music;

    /// <summary>
    /// <paramref name="conectar"/> fica em falso de propósito: só o /play me arrasta pra
    /// dentro de um canal. Mandar pausar o que não existe não devia me fazer aparecer.
    /// </summary>
    private protected async Task ExecutarAsync(
        Func<AlephPlayer, Task<Music.Resposta>> ação,
        bool conectar = false,
        bool exigirMesmoCanal = true)
    {
        var (player, erro) = await Music.PlayerAsync(
            Áudio, Context, Context.Channel, conectar, exigirMesmoCanal);

        if (erro is not null)
        {
            await ErrorAsync(erro);
            return;
        }

        var resposta = await ação(player!);

        if (resposta.Erro is { } falha)
            await ErrorAsync(falha);
        else
            await RespondAsync(resposta.Embed!);
    }
}

/// <summary>Base dos gêmeos com prefixo. Mesma dança, resposta por mensagem em vez de interação.</summary>
public abstract class MusicTextModule : AlephTextModule
{
    protected MusicTextModule(IAudioService áudio)
    {
        Áudio = áudio;
    }

    protected IAudioService Áudio { get; }

    public override CommandCategory Category => CommandCategory.Music;

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    private protected async Task ExecutarAsync(
        Func<AlephPlayer, Task<Music.Resposta>> ação,
        bool conectar = false,
        bool exigirMesmoCanal = true)
    {
        var (player, erro) = await Music.PlayerAsync(
            Áudio, Context, Context.Channel, conectar, exigirMesmoCanal);

        if (erro is not null)
        {
            await ErrorAsync(erro);
            return;
        }

        var resposta = await ação(player!);

        if (resposta.Erro is { } falha)
            await ErrorAsync(falha);
        else
            await ReplyAsync(resposta.Embed!);
    }
}

using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Contratos;

using Lavalink4NET;

namespace AlephBot.Core.Commands.Music;

// Base dos comandos de música em barra: pega o player, desiste com uma frase, executa, responde.
public abstract class MusicSlashModule : AlephSlashModule
{
    protected MusicSlashModule(IAudioService áudio)
    {
        Áudio = áudio;
    }

    protected IAudioService Áudio { get; }

    public override CommandCategory Category => CommandCategory.Music;

    // conectar fica em falso: só o /play me arrasta pra dentro de um canal de voz
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
        {
            await ErrorAsync(falha);
        }
        else if (resposta.Cartão is { } cartão)
        {
            await RespondAsync(cartão.Container, cartão.Anexo);
        }
        else
        {
            await RespondAsync(resposta.Embed!);

            // o que ficou pro depois roda com a resposta publicada: o "Pulei" vem antes
            if (resposta.Depois is { } depois)
                await depois();
        }

        // a ação pode ter mexido em fila, pausa, volume ou loop: a foto é o que sobrevive a restart
        player!.Fotografar();
    }
}

// Base dos gêmeos com prefixo. Mesma dança, resposta por mensagem em vez de interação.
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
        {
            await ErrorAsync(falha);
        }
        else if (resposta.Cartão is { } cartão)
        {
            await ReplyAsync(cartão.Container, cartão.Anexo);
        }
        else
        {
            await ReplyAsync(resposta.Embed!);

            if (resposta.Depois is { } depois)
                await depois();
        }

        player!.Fotografar();
    }
}

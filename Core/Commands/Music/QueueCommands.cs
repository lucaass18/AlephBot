using AlephBot.Core.Personality;

using Lavalink4NET;
using Lavalink4NET.Players.Queued;

using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Music;

/// <summary>Como o usuário escolhe o modo de repetição — o /loop mostra estas três opções.</summary>
public enum ModoDeRepetição
{
    [SlashCommandChoice(Name = "desligado")]
    Desligado,

    [SlashCommandChoice(Name = "faixa")]
    Faixa,

    [SlashCommandChoice(Name = "fila")]
    Fila,
}

/// <summary>O que se faz com a fila: olhar, embaralhar e escolher como ela se repete.</summary>
internal static class Fila
{
    internal static Task<Music.Resposta> MostrarAsync(AlephPlayer player) =>
        Task.FromResult(player.CurrentTrack is null && player.Queue.IsEmpty
            ? Music.Resposta.Falha(Denia.MúsicaFilaVazia())
            : Music.Resposta.Ok(Music.EmbedFila(player)));

    internal static Task<Music.Resposta> AgoraAsync(AlephPlayer player) =>
        Task.FromResult(player.CurrentTrack is { } faixa
            ? Music.Resposta.Ok(Music.EmbedProgresso(player, faixa))
            : Music.Resposta.Falha(Denia.MúsicaNãoEstouTocando()));

    internal static async Task<Music.Resposta> EmbaralharAsync(AlephPlayer player)
    {
        var quantas = player.Queue.Count;

        // com uma faixa só a ordem já é a única possível; embaralhar seria mentira
        if (quantas < 2)
            return Music.Resposta.Falha(Denia.MúsicaFilaCurtaDemais());

        await player.Queue.ShuffleAsync();

        return Music.Resposta.Ok(Music.Aviso($"🔀 {Denia.MúsicaEmbaralhou(quantas)}"));
    }

    internal static Task<Music.Resposta> RepetirAsync(AlephPlayer player, ModoDeRepetição modo)
    {
        player.RepeatMode = modo switch
        {
            ModoDeRepetição.Faixa => TrackRepeatMode.Track,
            ModoDeRepetição.Fila => TrackRepeatMode.Queue,
            _ => TrackRepeatMode.None,
        };

        var texto = modo switch
        {
            // pedir repetição sem nada tocando vale: fica pra próxima. só não tenho nome pra dizer
            ModoDeRepetição.Faixa => Denia.MúsicaLoopFaixa(
                Music.Escapa(player.CurrentTrack?.Title ?? Denia.MúsicaTítuloTocando)),
            ModoDeRepetição.Fila => Denia.MúsicaLoopFila(),
            _ => Denia.MúsicaLoopDesligado(),
        };

        return Task.FromResult(Music.Resposta.Ok(
            Music.Aviso($"{Music.Repetição(player.RepeatMode)} {texto}")));
    }
}

/// <summary>/queue</summary>
public sealed class QueueCommand : MusicSlashModule
{
    public QueueCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "queue";
    public override string Description => "Mostra a fila de músicas.";
    public override string? Usage => "queue";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("queue", "Mostra a fila de músicas.")]
    public Task QueueAsync() => ExecutarAsync(Fila.MostrarAsync, exigirMesmoCanal: false);
}

/// <summary>!queue</summary>
public sealed class QueueTextCommand : MusicTextModule
{
    public QueueTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "queue";
    public override string Description => "Mostra a fila de músicas.";
    public override string? Usage => "queue";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("queue", "q", "fila")]
    public Task QueueAsync() => ExecutarAsync(Fila.MostrarAsync, exigirMesmoCanal: false);
}

/// <summary>/nowplaying</summary>
public sealed class NowPlayingCommand : MusicSlashModule
{
    public NowPlayingCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "nowplaying";
    public override string Description => "Mostra a faixa atual e quanto já rolou dela.";
    public override string? Usage => "nowplaying";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("nowplaying", "Mostra a faixa atual e quanto já rolou dela.")]
    public Task NowPlayingAsync() => ExecutarAsync(Fila.AgoraAsync, exigirMesmoCanal: false);
}

/// <summary>!nowplaying</summary>
public sealed class NowPlayingTextCommand : MusicTextModule
{
    public NowPlayingTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "nowplaying";
    public override string Description => "Mostra a faixa atual e quanto já rolou dela.";
    public override string? Usage => "nowplaying";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("nowplaying", "np", "agora", "tocando")]
    public Task NowPlayingAsync() => ExecutarAsync(Fila.AgoraAsync, exigirMesmoCanal: false);
}

/// <summary>/shuffle</summary>
public sealed class ShuffleCommand : MusicSlashModule
{
    public ShuffleCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "shuffle";
    public override string Description => "Embaralha a fila.";
    public override string? Usage => "shuffle";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("shuffle", "Embaralha a fila.")]
    public Task ShuffleAsync() => ExecutarAsync(Fila.EmbaralharAsync);
}

/// <summary>!shuffle</summary>
public sealed class ShuffleTextCommand : MusicTextModule
{
    public ShuffleTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "shuffle";
    public override string Description => "Embaralha a fila.";
    public override string? Usage => "shuffle";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("shuffle", "embaralhar")]
    public Task ShuffleAsync() => ExecutarAsync(Fila.EmbaralharAsync);
}

/// <summary>/loop</summary>
public sealed class LoopCommand : MusicSlashModule
{
    public LoopCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "loop";
    public override string Description => "Escolhe se a faixa, a fila, ou nada se repete.";
    public override string? Usage => "loop <desligado|faixa|fila>";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("loop", "Escolhe se a faixa, a fila, ou nada se repete.")]
    public Task LoopAsync(
        [SlashCommandParameter(Name = "modo", Description = "O que deve se repetir.")] ModoDeRepetição modo) =>
        ExecutarAsync(player => Fila.RepetirAsync(player, modo));
}

/// <summary>!loop</summary>
public sealed class LoopTextCommand : MusicTextModule
{
    public LoopTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "loop";
    public override string Description => "Escolhe se a faixa, a fila, ou nada se repete.";
    public override string? Usage => "loop <desligado|faixa|fila>";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("loop", "repeat", "repetir")]
    public Task LoopAsync(string modo) =>
        Ler(modo) is { } escolhido
            ? ExecutarAsync(player => Fila.RepetirAsync(player, escolhido))
            : ErrorAsync(Denia.NãoEntendi(Usage!));

    /// <summary>
    /// O slash tem menu de escolha; aqui vem texto livre, então aceito os dois idiomas —
    /// quem escreve `!loop off` e quem escreve `!loop fila` quer a mesma coisa.
    /// </summary>
    private static ModoDeRepetição? Ler(string modo) => modo.Trim().ToLowerInvariant() switch
    {
        "off" or "no" or "nao" or "não" or "desligado" or "desligar" or "none" => ModoDeRepetição.Desligado,
        "faixa" or "musica" or "música" or "track" or "song" or "one" or "1" => ModoDeRepetição.Faixa,
        "fila" or "queue" or "all" or "tudo" => ModoDeRepetição.Fila,
        _ => null,
    };
}

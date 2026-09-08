using AlephBot.Core.Personality;

using Lavalink4NET;

using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// Os comandos que só mexem no que já está tocando: pular, parar, sair, pausar e voltar.
/// Cada ação existe uma vez só aqui; as classes abaixo são as duas portas de entrada
/// (/comando e !comando) pra ela.
/// </summary>
internal static class Playback
{
    internal static async Task<Music.Resposta> PularAsync(AlephPlayer player)
    {
        if (player.CurrentTrack is not { } atual)
            return Music.Resposta.Falha(Denia.MúsicaNãoEstouTocando());

        var temPróxima = player.Queue.Count > 0;

        await player.SkipAsync();

        return Music.Resposta.Ok(Music.Aviso($"⏭️ {Denia.MúsicaPulou(Music.Escapa(atual.Title), temPróxima)}"));
    }

    /// <summary>Para e esvazia a fila, mas continua no canal — quem me tira é o /disconnect.</summary>
    internal static async Task<Music.Resposta> PararAsync(AlephPlayer player)
    {
        // a fila sai primeiro: parar com faixa esperando faria o player emendar na próxima
        await player.Queue.ClearAsync();
        await player.StopAsync();

        return Music.Resposta.Ok(Music.Aviso($"⏹️ {Denia.MúsicaParou()}"));
    }

    internal static async Task<Music.Resposta> SairAsync(AlephPlayer player)
    {
        await player.Queue.ClearAsync();
        await player.DisconnectAsync();

        return Music.Resposta.Ok(Music.Aviso($"👋 {Denia.MúsicaSaiu()}"));
    }

    internal static async Task<Music.Resposta> PausarAsync(AlephPlayer player)
    {
        if (player.CurrentTrack is not { } atual)
            return Music.Resposta.Falha(Denia.MúsicaNãoEstouTocando());

        if (player.IsPaused)
            return Music.Resposta.Falha(Denia.MúsicaJáPausada());

        await player.PauseAsync();

        return Music.Resposta.Ok(Music.Aviso($"⏸️ {Denia.MúsicaPausou(Music.Escapa(atual.Title))}"));
    }

    internal static async Task<Music.Resposta> VoltarAsync(AlephPlayer player)
    {
        if (player.CurrentTrack is not { } atual)
            return Music.Resposta.Falha(Denia.MúsicaNãoEstouTocando());

        if (!player.IsPaused)
            return Music.Resposta.Falha(Denia.MúsicaNãoEstavaPausada());

        await player.ResumeAsync();

        return Music.Resposta.Ok(Music.Aviso($"▶️ {Denia.MúsicaVoltou(Music.Escapa(atual.Title))}"));
    }
}

/// <summary>/skip</summary>
public sealed class SkipCommand : MusicSlashModule
{
    public SkipCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "skip";
    public override string Description => "Pula a faixa que está tocando.";
    public override string? Usage => "skip";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("skip", "Pula a faixa que está tocando.")]
    public Task SkipAsync() => ExecutarAsync(Playback.PularAsync);
}

/// <summary>!skip</summary>
public sealed class SkipTextCommand : MusicTextModule
{
    public SkipTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "skip";
    public override string Description => "Pula a faixa que está tocando.";
    public override string? Usage => "skip";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("skip", "s", "pular", "next")]
    public Task SkipAsync() => ExecutarAsync(Playback.PularAsync);
}

/// <summary>/stop</summary>
public sealed class StopCommand : MusicSlashModule
{
    public StopCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "stop";
    public override string Description => "Para a música e limpa a fila.";
    public override string? Usage => "stop";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("stop", "Para a música e limpa a fila.")]
    public Task StopAsync() => ExecutarAsync(Playback.PararAsync);
}

/// <summary>!stop</summary>
public sealed class StopTextCommand : MusicTextModule
{
    public StopTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "stop";
    public override string Description => "Para a música e limpa a fila.";
    public override string? Usage => "stop";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("stop", "parar")]
    public Task StopAsync() => ExecutarAsync(Playback.PararAsync);
}

/// <summary>/disconnect</summary>
public sealed class DisconnectCommand : MusicSlashModule
{
    public DisconnectCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "disconnect";
    public override string Description => "Me tira do canal de voz.";
    public override string? Usage => "disconnect";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("disconnect", "Me tira do canal de voz.")]
    public Task DisconnectAsync() => ExecutarAsync(Playback.SairAsync);
}

/// <summary>!disconnect</summary>
public sealed class DisconnectTextCommand : MusicTextModule
{
    public DisconnectTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "disconnect";
    public override string Description => "Me tira do canal de voz.";
    public override string? Usage => "disconnect";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("disconnect", "leave", "sair", "dc")]
    public Task DisconnectAsync() => ExecutarAsync(Playback.SairAsync);
}

/// <summary>/pause</summary>
public sealed class PauseCommand : MusicSlashModule
{
    public PauseCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "pause";
    public override string Description => "Pausa a faixa que está tocando.";
    public override string? Usage => "pause";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("pause", "Pausa a faixa que está tocando.")]
    public Task PauseAsync() => ExecutarAsync(Playback.PausarAsync);
}

/// <summary>!pause</summary>
public sealed class PauseTextCommand : MusicTextModule
{
    public PauseTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "pause";
    public override string Description => "Pausa a faixa que está tocando.";
    public override string? Usage => "pause";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("pause", "pausar")]
    public Task PauseAsync() => ExecutarAsync(Playback.PausarAsync);
}

/// <summary>/resume</summary>
public sealed class ResumeCommand : MusicSlashModule
{
    public ResumeCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "resume";
    public override string Description => "Volta a tocar depois do pause.";
    public override string? Usage => "resume";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("resume", "Volta a tocar depois do pause.")]
    public Task ResumeAsync() => ExecutarAsync(Playback.VoltarAsync);
}

/// <summary>!resume</summary>
public sealed class ResumeTextCommand : MusicTextModule
{
    public ResumeTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "resume";
    public override string Description => "Volta a tocar depois do pause.";
    public override string? Usage => "resume";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("resume", "voltar", "continuar")]
    public Task ResumeAsync() => ExecutarAsync(Playback.VoltarAsync);
}

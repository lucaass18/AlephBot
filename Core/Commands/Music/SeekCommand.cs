using AlephBot.Core.Personality;

using Lavalink4NET;

using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Music;

/// <summary>/seek — anda pra um ponto da faixa atual.</summary>
public sealed class SeekCommand : MusicSlashModule
{
    public SeekCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "seek";
    public override string Description => "Anda pra um ponto da faixa atual.";
    public override string? Usage => "seek <posição>";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("seek", "Anda pra um ponto da faixa atual.")]
    public Task SeekAsync(
        [SlashCommandParameter(Name = "posicao", Description = "Onde parar: 1:30, 1:02:03, 90 ou 1m30s.")] string posição) =>
        ExecutarAsync(player => Seek.IrParaAsync(player, posição));
}

/// <summary>!seek</summary>
public sealed class SeekTextCommand : MusicTextModule
{
    public SeekTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "seek";
    public override string Description => "Anda pra um ponto da faixa atual.";
    public override string? Usage => "seek <posição>";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("seek", "ir", "pular-para")]
    public Task SeekAsync(string posição) =>
        ExecutarAsync(player => Seek.IrParaAsync(player, posição));
}

internal static class Seek
{
    internal static async Task<Music.Resposta> IrParaAsync(AlephPlayer player, string texto)
    {
        if (player.CurrentTrack is not { } atual)
            return Music.Resposta.Falha(Denia.MúsicaNãoEstouTocando());

        // rádio e live não têm linha do tempo: não existe "meio" de uma coisa que não acaba
        if (atual.IsLiveStream || !atual.IsSeekable)
            return Music.Resposta.Falha(Denia.MúsicaNãoDáProProcurar(Music.Escapa(atual.Title)));

        if (Music.ParsePosição(texto) is not { } posição)
            return Music.Resposta.Falha(Denia.MúsicaPosiçãoInválida(Music.ExemploPosição));

        if (posição > atual.Duration)
            return Music.Resposta.Falha(Denia.MúsicaPosiçãoLongaDemais(Music.Duração(atual.Duration)));

        await player.SeekAsync(posição);

        return Music.Resposta.Ok(Music.Aviso(
            $"⏩ {Denia.MúsicaPulouPara(Music.Escapa(atual.Title), Music.Duração(posição))}"));
    }
}

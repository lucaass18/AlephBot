using AlephBot.Core.Personality;

using Lavalink4NET;

using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// /volume — com número, muda; sem número, só diz onde está. O Lavalink aceita volume
/// muito além de 200%, mas ali em cima já é distorção, não música.
/// </summary>
public sealed class VolumeCommand : MusicSlashModule
{
    public VolumeCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "volume";
    public override string Description => "Mostra ou muda o volume.";
    public override string? Usage => "volume [0-200]";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [SlashCommand("volume", "Mostra ou muda o volume.")]
    public Task VolumeAsync(
        [SlashCommandParameter(Name = "porcento", Description = "De 0 a 200. Sem isto, eu só digo onde está.")] int? porcento = null) =>
        ExecutarAsync(player => Volume.AjustarAsync(player, porcento));
}

/// <summary>!volume</summary>
public sealed class VolumeTextCommand : MusicTextModule
{
    public VolumeTextCommand(IAudioService áudio) : base(áudio) { }

    public override string Name => "volume";
    public override string Description => "Mostra ou muda o volume.";
    public override string? Usage => "volume [0-200]";

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [Command("volume", "vol", "v")]
    public Task VolumeAsync(int? porcento = null) =>
        ExecutarAsync(player => Volume.AjustarAsync(player, porcento));
}

internal static class Volume
{
    internal static async Task<Music.Resposta> AjustarAsync(AlephPlayer player, int? porcento)
    {
        if (porcento is not { } valor)
            return Music.Resposta.Ok(Music.Aviso($"🔊 {Denia.MúsicaVolumeMudou(Music.Porcento(player.Volume))}"));

        if (valor is < 0 or > Music.VolumeMáximo)
            return Music.Resposta.Falha(Denia.MúsicaVolumeInválido(Music.VolumeMáximo));

        // o Lavalink trabalha com 1.0 = 100%
        await player.SetVolumeAsync(valor / 100f);

        return Music.Resposta.Ok(Music.Aviso($"🔊 {Denia.MúsicaVolumeMudou(valor)}"));
    }
}

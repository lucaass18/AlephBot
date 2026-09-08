using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Interaction;

/// <summary>/ping</summary>
public sealed class PingCommand : AlephSlashModule
{
    public override string Name => "ping";
    public override string Description => "Mostra a latência do bot.";
    public override CommandCategory Category => CommandCategory.Utility;
    public override string? Usage => "ping";

    [SlashCommand("ping", "Mostra a latência do bot.")]
    public Task PingAsync() =>
        RespondAsync(BuildEmbed(Context.Client.Latency));

    // a barra enche onde a cor fica vermelha, senão as duas contam histórias diferentes
    private const int GaugeLength = 8;
    private const double SlowMs = 300;

    // texto de embed não tem cor: quem muda de cor é o emoji do quadrado
    private const string Empty = "⬛";

    internal static EmbedProperties BuildEmbed(TimeSpan latency)
    {
        var ms = latency.TotalMilliseconds;

        return new EmbedProperties
        {
            Title = "🏓 Pong!",
            Description = $"{Gauge(ms)}  **{ms:F0} ms**\n{FlavorFor(ms)}",
            Color = new Color(ColorFor(ms)),
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static string Gauge(double ms)
    {
        var filled = Math.Clamp((int)Math.Ceiling(ms / SlowMs * GaugeLength), 1, GaugeLength);
        var block = BlockFor(ms);

        return string.Concat(
            string.Concat(Enumerable.Repeat(block, filled)),
            string.Concat(Enumerable.Repeat(Empty, GaugeLength - filled)));
    }

    private static string BlockFor(double ms) => ms switch
    {
        < 150 => "🟩",
        < SlowMs => "🟨",
        _ => "🟥",
    };

    private static int ColorFor(double ms) => ms switch
    {
        < 150 => 0x2ECC71,
        < SlowMs => 0xF1C40F,
        _ => 0xE74C3C,
    };

    private static string FlavorFor(double ms) => ms switch
    {
        < 150 => Denia.PingRápido(),
        < SlowMs => Denia.PingNormal(),
        _ => Denia.PingLento(),
    };
}

/// <summary>!ping</summary>
public sealed class PingTextCommand : AlephTextModule
{
    public override string Name => "ping";
    public override string Description => "Mostra a latência do bot.";
    public override CommandCategory Category => CommandCategory.Utility;
    public override string? Usage => "ping";

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [Command("ping")]
    public Task PingAsync() =>
        ReplyAsync(PingCommand.BuildEmbed(Context.Client.Latency));
}

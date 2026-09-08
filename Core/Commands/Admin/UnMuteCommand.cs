using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Admin;

/// <summary>
/// /unmute — tira o timeout antes da hora. O silêncio do /mute expira sozinho,
/// então isto serve para encurtar um castigo, não para consertar um vazamento.
/// </summary>
public sealed class UnMuteCommand : AlephSlashModule
{
    private const AçãoModeração Ação = AçãoModeração.Unmute;

    public override string Name => "unmute";
    public override string Description => "Devolve a voz a quem está silenciado.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "unmute <@usuário> [motivo]";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ModerateUsers)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.ModerateUsers)]
    [SlashCommand("unmute", "Devolve a voz a quem está silenciado.")]
    public async Task UnMuteAsync(
        [SlashCommandParameter(Name = "usuario", Description = "Quem volta a falar.")] GuildUser alvo,
        [SlashCommandParameter(Name = "motivo", Description = "Por que o silêncio acabou mais cedo.")] string? motivo = null)
    {
        if (Context.Guild is not { } guild)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        if (Context.User is not GuildUser moderador)
        {
            await ErrorAsync(Denia.NãoIdentifiquei());
            return;
        }

        if (Moderation.Validate(guild, alvo, moderador, Context.Client.Id, Ação) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        if (RestoAtéSoltar(alvo) is not { } restante)
        {
            await ErrorAsync(Denia.NãoEstáSilenciado(alvo.ToString()));
            return;
        }

        motivo = Moderation.NormalizeReason(motivo);

        var extra = MuteCommand.Humanize(restante);

        if (await TryUnMuteAsync(alvo, moderador, motivo) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        // aqui a DM vai depois: o alvo continua no servidor, e não faz sentido
        // avisar que soltei antes de ter soltado de verdade
        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivo, Ação, extra);

        await RespondAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivo, extra));
    }

    /// <summary>Devolve a mensagem de erro, ou null se o silêncio foi levantado.</summary>
    internal static async Task<string?> TryUnMuteAsync(GuildUser alvo, GuildUser moderador, string motivo)
    {
        try
        {
            // TimeOutAsync só aceita DateTimeOffset não-nulo, então não dá pra
            // "desligar" por ali: quem zera o timeout é o ModifyAsync
            await alvo.ModifyAsync(
                opções => opções.TimeOutUntil = null,
                Moderation.AuditLog(moderador, motivo));

            return null;
        }
        catch (RestException ex)
        {
            return Denia.Falhou(AçãoModeração.Unmute, alvo.ToString(), ex.StatusCode.ToString());
        }
    }

    /// <summary>
    /// Quanto ainda faltava do castigo, ou null se o usuário não está calado.
    /// O Discord mantém a data preenchida mesmo depois de expirar, por isso a
    /// comparação com o agora — sem ela, todo mundo já silenciado um dia parece mudo.
    /// </summary>
    internal static TimeSpan? RestoAtéSoltar(GuildUser alvo)
    {
        if (alvo.TimeOutUntil is not { } até)
            return null;

        var restante = até - DateTimeOffset.UtcNow;

        return restante > TimeSpan.Zero ? restante : null;
    }
}

/// <summary>!unmute — mesmo alívio do /unmute, para quem prefere o prefixo.</summary>
public sealed class UnMuteTextCommand : AlephTextModule
{
    private const AçãoModeração Ação = AçãoModeração.Unmute;

    public override string Name => "unmute";
    public override string Description => "Devolve a voz a quem está silenciado.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "unmute <@usuário> [motivo]";

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<CommandContext>(Permissions.ModerateUsers)]
    [RequireBotPermissions<CommandContext>(Permissions.ModerateUsers)]
    [Command("unmute", "desmutar", "dessilenciar")]
    public async Task UnMuteAsync(
        GuildUser alvo,
        [CommandParameter(Remainder = true)] string? motivo = null)
    {
        if (Context.Guild is not { } guild)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        var moderador = await Context.Client.Rest.GetGuildUserAsync(guild.Id, Context.User.Id);

        if (Moderation.Validate(guild, alvo, moderador, Context.Client.Id, Ação) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        if (UnMuteCommand.RestoAtéSoltar(alvo) is not { } restante)
        {
            await ErrorAsync(Denia.NãoEstáSilenciado(alvo.ToString()));
            return;
        }

        var motivoFinal = Moderation.NormalizeReason(motivo);

        var extra = MuteCommand.Humanize(restante);

        if (await UnMuteCommand.TryUnMuteAsync(alvo, moderador, motivoFinal) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivoFinal, Ação, extra);

        await ReplyAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivoFinal, extra));
    }
}
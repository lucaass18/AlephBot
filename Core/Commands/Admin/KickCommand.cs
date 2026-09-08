using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Admin;

/// <summary>/kick — expulsa um usuário do servidor e registra quem expulsou e o motivo.</summary>
public sealed class KickCommand : AlephSlashModule
{
    private const AçãoModeração Ação = AçãoModeração.Kick;

    public override string Name => "kick";
    public override string Description => "Expulsa um usuário do servidor.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "kick <@usuário> [motivo]";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.KickUsers)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.KickUsers)]
    [SlashCommand("kick", "Expulsa um usuário do servidor.")]
    public async Task KickAsync(
        [SlashCommandParameter(Name = "usuario", Description = "Quem será expulso.")] GuildUser alvo,
        [SlashCommandParameter(Name = "motivo", Description = "Motivo da expulsão.")] string? motivo = null)
    {
        if (Context.Guild is not { } guild)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        // dentro de servidor isso sempre passa; o cast é só pra eu enxergar os cargos dele
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

        motivo = Moderation.NormalizeReason(motivo);

        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivo, Ação);

        if (await TryKickAsync(alvo, moderador, motivo) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        await RespondAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivo));
    }

    /// <summary>Devolve a mensagem de erro, ou null se o kick foi em frente.</summary>
    internal static async Task<string?> TryKickAsync(GuildUser alvo, GuildUser moderador, string motivo)
    {
        try
        {
            await alvo.KickAsync(Moderation.AuditLog(moderador, motivo));

            return null;
        }
        catch (RestException ex)
        {
            return Denia.Falhou(AçãoModeração.Kick, alvo.ToString(), ex.StatusCode.ToString());
        }
    }
}

/// <summary>!kick — mesma expulsão do /kick, para quem prefere o prefixo.</summary>
public sealed class KickTextCommand : AlephTextModule
{
    private const AçãoModeração Ação = AçãoModeração.Kick;

    public override string Name => "kick";
    public override string Description => "Expulsa um usuário do servidor.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "kick <@usuário> [motivo]";

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<CommandContext>(Permissions.KickUsers)]
    [RequireBotPermissions<CommandContext>(Permissions.KickUsers)]
    [Command("kick", "expulsar")]
    public async Task KickAsync(
        GuildUser alvo,
        [CommandParameter(Remainder = true)] string? motivo = null)
    {
        if (Context.Guild is not { } guild)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        // aqui o autor vem como User (Message.Author), então busco o membro pra comparar cargos
        var moderador = await Context.Client.Rest.GetGuildUserAsync(guild.Id, Context.User.Id);

        if (Moderation.Validate(guild, alvo, moderador, Context.Client.Id, Ação) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        var motivoFinal = Moderation.NormalizeReason(motivo);

        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivoFinal, Ação);

        if (await KickCommand.TryKickAsync(alvo, moderador, motivoFinal) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        await ReplyAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivoFinal));
    }
}

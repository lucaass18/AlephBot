using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Admin;

/// <summary>
/// /unban — tira alguém da lista de banidos. Aqui o alvo não é GuildUser:
/// quem está banido não é membro do servidor, então o Discord não resolve menção
/// nem autocompleta. Só o ID serve.
/// </summary>
public sealed class UnBanCommand : AlephSlashModule
{
    private const AçãoModeração Ação = AçãoModeração.Unban;

    public override string Name => "unban";
    public override string Description => "Remove o banimento de um usuário.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "unban <id> [motivo]";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.BanUsers)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.BanUsers)]
    [SlashCommand("unban", "Remove o banimento de um usuário.")]
    public async Task UnBanAsync(
        [SlashCommandParameter(Name = "id", Description = "ID de quem será desbanido.")] string id,
        [SlashCommandParameter(Name = "motivo", Description = "Motivo do perdão.")] string? motivo = null)
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

        var (alvoId, erro) = Validate(id, moderador.Id, Context.Client.Id);

        if (erro is not null)
        {
            await ErrorAsync(erro);
            return;
        }

        motivo = Moderation.NormalizeReason(motivo);

        if (await ResolveBanAsync(guild, alvoId!.Value) is not { } ban)
        {
            await ErrorAsync(Denia.NãoEstáBanido($"`{alvoId}`"));
            return;
        }

        if (await TryUnBanAsync(guild, ban.User, moderador, motivo) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        // desbanido não compartilha servidor comigo, então a DM quase sempre falha —
        // TryNotifyAsync engole isso. Vale a tentativa: se tivermos outro servidor
        // em comum, o aviso chega.
        await Moderation.TryNotifyAsync(Context.Client, guild, ban.User, moderador, motivo, Ação);

        await RespondAsync(Moderation.BuildEmbed(Ação, ban.User, moderador, motivo, MotivoOriginal(ban)));
    }

    /// <summary>Valida o ID cru e as recusas que não dependem de cargo.</summary>
    internal static (ulong? Id, string? Erro) Validate(string id, ulong moderadorId, ulong botId)
    {
        if (ParseId(id) is not { } alvoId)
            return (null, Denia.IdInválido());

        if (alvoId == moderadorId)
            return (null, Denia.RecusaPróprio(AçãoModeração.Unban));

        if (alvoId == botId)
            return (null, Denia.RecusaBot(AçãoModeração.Unban));

        return (alvoId, null);
    }

    /// <summary>
    /// Aceita o ID cru e também a menção colada (&lt;@123&gt; / &lt;@!123&gt;), que é o que
    /// sai do "Copiar ID" quando o usuário ainda aparece em alguma mensagem antiga.
    /// </summary>
    internal static ulong? ParseId(string texto)
    {
        var limpo = texto.Trim().Trim('<', '>', '@', '!');

        return ulong.TryParse(limpo, out var id) ? id : null;
    }

    /// <summary>O banimento existente, ou null se não há banimento para esse ID.</summary>
    internal static async Task<GuildBan?> ResolveBanAsync(Guild guild, ulong alvoId)
    {
        try
        {
            return await guild.GetBanAsync(alvoId);
        }
        catch (RestException)
        {
            // 404 é o caso normal: simplesmente não está banido
            return null;
        }
    }

    /// <summary>Devolve a mensagem de erro, ou null se o banimento foi removido.</summary>
    internal static async Task<string?> TryUnBanAsync(
        Guild guild, User alvo, GuildUser moderador, string motivo)
    {
        try
        {
            await guild.UnbanUserAsync(alvo.Id, Moderation.AuditLog(moderador, motivo));

            return null;
        }
        catch (RestException ex)
        {
            return Denia.Falhou(AçãoModeração.Unban, alvo.ToString(), ex.StatusCode.ToString());
        }
    }

    /// <summary>Por que a pessoa tinha sido banida — o campo extra do embed.</summary>
    internal static string MotivoOriginal(GuildBan ban) =>
        string.IsNullOrWhiteSpace(ban.Reason) ? Denia.SemMotivo() : ban.Reason;
}

/// <summary>!unban — mesmo perdão do /unban, para quem prefere o prefixo.</summary>
public sealed class UnBanTextCommand : AlephTextModule
{
    private const AçãoModeração Ação = AçãoModeração.Unban;

    public override string Name => "unban";
    public override string Description => "Remove o banimento de um usuário.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "unban <id> [motivo]";

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<CommandContext>(Permissions.BanUsers)]
    [RequireBotPermissions<CommandContext>(Permissions.BanUsers)]
    [Command("unban", "desbanir")]
    public async Task UnBanAsync(
        string id,
        [CommandParameter(Remainder = true)] string? motivo = null)
    {
        if (Context.Guild is not { } guild)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        var moderador = await Context.Client.Rest.GetGuildUserAsync(guild.Id, Context.User.Id);

        var (alvoId, erro) = UnBanCommand.Validate(id, moderador.Id, Context.Client.Id);

        if (erro is not null)
        {
            await ErrorAsync(erro);
            return;
        }

        if (await UnBanCommand.ResolveBanAsync(guild, alvoId!.Value) is not { } ban)
        {
            await ErrorAsync(Denia.NãoEstáBanido($"`{alvoId}`"));
            return;
        }

        var motivoFinal = Moderation.NormalizeReason(motivo);

        if (await UnBanCommand.TryUnBanAsync(guild, ban.User, moderador, motivoFinal) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        await Moderation.TryNotifyAsync(Context.Client, guild, ban.User, moderador, motivoFinal, Ação);

        await ReplyAsync(Moderation.BuildEmbed(Ação, ban.User, moderador, motivoFinal, UnBanCommand.MotivoOriginal(ban)));
    }
}
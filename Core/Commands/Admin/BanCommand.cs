using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Admin;

/// <summary>/ban — bane um usuário e, opcionalmente, apaga as mensagens recentes dele.</summary>
public sealed class BanCommand : AlephSlashModule
{
    private const AçãoModeração Ação = AçãoModeração.Ban;

    // limite do Discord para apagar mensagens junto do ban
    internal const int MaxDias = 7;

    public override string Name => "ban";
    public override string Description => "Bane um usuário do servidor.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "ban <@usuário> [motivo] [apagar]";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.BanUsers)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.BanUsers)]
    [SlashCommand("ban", "Bane um usuário do servidor.")]
    public async Task BanAsync(
        [SlashCommandParameter(Name = "usuario", Description = "Quem será banido.")] GuildUser alvo,
        [SlashCommandParameter(Name = "motivo", Description = "Motivo do banimento.")] string? motivo = null,
        [SlashCommandParameter(Name = "apagar", Description = "Dias de mensagens para apagar (0 a 7). Padrão: 0.")] int apagarDias = 0)
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

        if (apagarDias is < 0 or > MaxDias)
        {
            await ErrorAsync(Denia.ApagarInválido());
            return;
        }

        if (Moderation.Validate(guild, alvo, moderador, Context.Client.Id, Ação) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        motivo = Moderation.NormalizeReason(motivo);

        var extra = DescreveLimpeza(apagarDias);

        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivo, Ação);

        if (await TryBanAsync(alvo, moderador, motivo, apagarDias) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        await RespondAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivo, extra));
    }

    /// <summary>Devolve a mensagem de erro, ou null se o ban foi aplicado.</summary>
    internal static async Task<string?> TryBanAsync(
        GuildUser alvo, GuildUser moderador, string motivo, int apagarDias)
    {
        try
        {
            // a API conta em segundos, não em dias
            var segundos = (int)TimeSpan.FromDays(apagarDias).TotalSeconds;

            await alvo.BanAsync(segundos, Moderation.AuditLog(moderador, motivo));

            return null;
        }
        catch (RestException ex)
        {
            return Denia.Falhou(AçãoModeração.Ban, alvo.ToString(), ex.StatusCode.ToString());
        }
    }

    internal static string DescreveLimpeza(int dias) => dias switch
    {
        0 => "Nenhuma",
        1 => "Último dia",
        _ => $"Últimos {dias} dias",
    };
}

/// <summary>!ban — mesmo banimento do /ban, para quem prefere o prefixo.</summary>
public sealed class BanTextCommand : AlephTextModule
{
    private const AçãoModeração Ação = AçãoModeração.Ban;

    public override string Name => "ban";
    public override string Description => "Bane um usuário do servidor.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "ban <@usuário> [motivo]";

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<CommandContext>(Permissions.BanUsers)]
    [RequireBotPermissions<CommandContext>(Permissions.BanUsers)]
    [Command("ban", "banir")]
    public async Task BanAsync(
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

        var motivoFinal = Moderation.NormalizeReason(motivo);

        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivoFinal, Ação);

        // pelo prefixo não dá pra escolher a limpeza: mantém as mensagens
        if (await BanCommand.TryBanAsync(alvo, moderador, motivoFinal, 0) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        await ReplyAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivoFinal, BanCommand.DescreveLimpeza(0)));
    }
}

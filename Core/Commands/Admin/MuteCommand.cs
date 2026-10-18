using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Contratos;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Admin;

// /mute — o timeout do Discord: ele para de falar até a hora marcada, e isto expira sozinho.
public sealed class MuteCommand : AlephSlashModule
{
    private const AçãoModeração Ação = AçãoModeração.Mute;

    // limite do Discord para o timeout
    internal const int MaxDias = 28;

    // exemplo mostrado quando a duração não faz sentido
    internal const string Exemplo = "30m";

    // acima disso o TimeSpan estoura — e quem pediu tanto já vai ouvir que passou do limite
    private static readonly TimeSpan Teto = TimeSpan.FromDays(365);

    public override string Name => "mute";
    public override string Description => "Silencia um usuário no texto e na voz.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "mute <@usuário> <duração> [motivo]";

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ModerateUsers)]
    [RequireBotPermissions<ApplicationCommandContext>(Permissions.ModerateUsers)]
    [SlashCommand("mute", "Silencia um usuário no texto e na voz.")]
    public async Task MuteAsync(
        [SlashCommandParameter(Name = "usuario", Description = "Quem será silenciado.")] GuildUser alvo,
        [SlashCommandParameter(Name = "duracao", Description = "Quanto tempo de silêncio: 30s, 10m, 2h, 7d.")] string duração,
        [SlashCommandParameter(Name = "motivo", Description = "Motivo do silêncio.")] string? motivo = null)
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

        if (ValidateDuração(duração) is not { } tempo)
        {
            await ErrorAsync(ErroDaDuração(duração));
            return;
        }

        if (Moderation.Validate(guild, alvo, moderador, Context.Client.Id, Ação) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        motivo = Moderation.NormalizeReason(motivo);

        var até = DateTimeOffset.UtcNow + tempo;

        if (await TryMuteAsync(alvo, moderador, motivo, até) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        var extra = DescreveSilêncio(tempo, até);

        // aviso depois: ele continua no servidor, e eu não anuncio castigo que ainda podia falhar
        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivo, Ação, extra);

        await RespondAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivo, extra));
    }

    // Devolve a mensagem de erro, ou null se o silêncio foi aplicado.
    internal static async Task<string?> TryMuteAsync(
        GuildUser alvo, GuildUser moderador, string motivo, DateTimeOffset até)
    {
        try
        {
            await alvo.TimeOutAsync(até, Moderation.AuditLog(moderador, motivo));

            return null;
        }
        catch (RestException ex)
        {
            return Denia.Falhou(AçãoModeração.Mute, alvo.ToString(), ex.StatusCode.ToString());
        }
    }

    // A duração pedida, ou null se não dá pra usar (inválida ou acima do limite).
    internal static TimeSpan? ValidateDuração(string texto) =>
        ParseDuração(texto) is { } tempo && tempo <= TimeSpan.FromDays(MaxDias) ? tempo : null;

    // Qual das duas reclamações cabe: texto que não entendi ou tempo longo demais.
    internal static string ErroDaDuração(string texto) =>
        ParseDuração(texto) is null ? Denia.DuraçãoInválida(Exemplo) : Denia.DuraçãoLongaDemais(MaxDias);

    // Lê "30s", "10m", "2h", "7d" e misturas. Número solto vira minutos; null quando não vira.
    internal static TimeSpan? ParseDuração(string texto)
    {
        var total = TimeSpan.Zero;
        long número = 0;
        var temNúmero = false;

        foreach (var c in texto)
        {
            if (char.IsWhiteSpace(c))
                continue;

            if (char.IsAsciiDigit(c))
            {
                número = número * 10 + (c - '0');
                temNúmero = true;

                // sete dígitos já passam muito do limite; corto aqui pra não estourar o TimeSpan
                if (número > 9_999_999)
                    return null;

                continue;
            }

            // unidade sem número na frente ("mh", "h30") não é duração, é engano
            if (!temNúmero)
                return null;

            var unidade = char.ToLowerInvariant(c) switch
            {
                's' => TimeSpan.FromSeconds(1),
                'm' => TimeSpan.FromMinutes(1),
                'h' => TimeSpan.FromHours(1),
                'd' => TimeSpan.FromDays(1),
                _ => TimeSpan.Zero,
            };

            if (unidade == TimeSpan.Zero)
                return null;

            total += unidade * número;
            número = 0;
            temNúmero = false;

            if (total > Teto)
                return Teto;
        }

        // número sem unidade no fim ("10", "1h30") fecha em minutos
        if (temNúmero)
            total += TimeSpan.FromMinutes(número);

        return total > TimeSpan.Zero ? total : null;
    }

    // O campo extra do embed: quanto tempo, e o relógio do Discord contando pro alvo.
    internal static string DescreveSilêncio(TimeSpan duração, DateTimeOffset até) =>
        $"{Humanize(duração)} • <t:{até.ToUnixTimeSeconds()}:R>";

    // TimeSpan em português, só as duas maiores unidades: "2 dias e 3 horas".
    internal static string Humanize(TimeSpan duração)
    {
        List<string> partes = [];

        Acrescenta(partes, duração.Days, "dia", "dias");
        Acrescenta(partes, duração.Hours, "hora", "horas");
        Acrescenta(partes, duração.Minutes, "minuto", "minutos");
        Acrescenta(partes, duração.Seconds, "segundo", "segundos");

        return partes.Count switch
        {
            0 => "menos de um segundo",
            1 => partes[0],
            _ => $"{partes[0]} e {partes[1]}",
        };
    }

    private static void Acrescenta(List<string> partes, int valor, string singular, string plural)
    {
        if (valor > 0)
            partes.Add($"{valor} {(valor == 1 ? singular : plural)}");
    }
}

// !mute — mesmo silêncio do /mute, para quem prefere o prefixo.
public sealed class MuteTextCommand : AlephTextModule
{
    private const AçãoModeração Ação = AçãoModeração.Mute;

    public override string Name => "mute";
    public override string Description => "Silencia um usuário no texto e na voz.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "mute <@usuário> <duração> [motivo]";

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [RequireContext<CommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<CommandContext>(Permissions.ModerateUsers)]
    [RequireBotPermissions<CommandContext>(Permissions.ModerateUsers)]
    [Command("mute", "mutar", "silenciar", "calar")]
    public async Task MuteAsync(
        GuildUser alvo,
        string duração,
        [CommandParameter(Remainder = true)] string? motivo = null)
    {
        if (Context.Guild is not { } guild)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        var moderador = await Context.Client.Rest.GetGuildUserAsync(guild.Id, Context.User.Id);

        if (MuteCommand.ValidateDuração(duração) is not { } tempo)
        {
            await ErrorAsync(MuteCommand.ErroDaDuração(duração));
            return;
        }

        if (Moderation.Validate(guild, alvo, moderador, Context.Client.Id, Ação) is { } erro)
        {
            await ErrorAsync(erro);
            return;
        }

        var motivoFinal = Moderation.NormalizeReason(motivo);

        var até = DateTimeOffset.UtcNow + tempo;

        if (await MuteCommand.TryMuteAsync(alvo, moderador, motivoFinal, até) is { } falha)
        {
            await ErrorAsync(falha);
            return;
        }

        var extra = MuteCommand.DescreveSilêncio(tempo, até);

        await Moderation.TryNotifyAsync(Context.Client, guild, alvo, moderador, motivoFinal, Ação, extra);

        await ReplyAsync(Moderation.BuildEmbed(Ação, alvo, moderador, motivoFinal, extra));
    }
}

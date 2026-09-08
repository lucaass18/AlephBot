using AlephBot.Core.Personality;

using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace AlephBot.Core.Commands.Admin;

/// <summary>
/// O que kick, mute e ban têm em comum: as recusas, a hierarquia de cargos,
/// o aviso na DM e o embed do resultado. Cada comando só cuida da própria ação.
/// </summary>
internal static class Moderation
{
    /// <summary>Devolve a mensagem de erro, ou null se a punição pode seguir.</summary>
    internal static string? Validate(
        Guild guild, GuildUser alvo, GuildUser moderador, ulong botId, AçãoModeração ação)
    {
        if (alvo.Id == moderador.Id)
            return Denia.RecusaPróprio(ação);

        if (alvo.Id == botId)
            return Denia.RecusaBot(ação);

        if (alvo.Id == guild.OwnerId)
            return Denia.RecusaDono();

        // hierarquia: quem chamou precisa estar acima do alvo (o dono passa por cima disso)
        if (moderador.Id != guild.OwnerId
            && TopRole(guild, alvo) is { } cargoAlvo
            && (TopRole(guild, moderador) is not { } cargoModerador || cargoModerador.Position <= cargoAlvo.Position))
        {
            return Denia.HierarquiaInsuficiente(alvo.ToString());
        }

        return null;
    }

    internal static string NormalizeReason(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) ? Denia.SemMotivo() : motivo.Trim();

    /// <summary>Motivo do audit log do Discord — quem olhar lá vê quem mandou e por quê.</summary>
    internal static RestRequestProperties AuditLog(GuildUser moderador, string motivo) =>
        new() { AuditLogReason = $"{moderador.Username}: {motivo}" };

    /// <summary>
    /// Avisa o punido antes da punição: depois do kick/ban o bot pode não conseguir
    /// mais abrir a DM. DM fechada não interrompe nada.
    /// </summary>
    internal static async Task TryNotifyAsync(
        GatewayClient client,
        Guild guild,
        User alvo,
        GuildUser moderador,
        string motivo,
        AçãoModeração ação,
        string? extra = null)
    {
        try
        {
            // GetDMChannelAsync abre a DM se ainda não existir
            var dm = await client.Rest.GetDMChannelAsync(alvo.Id);

            List<EmbedFieldProperties> fields =
            [
                new() { Name = "Administrador", Value = moderador.Username },
                new() { Name = "Motivo", Value = motivo },
            ];

            if (extra is not null)
                fields.Add(new EmbedFieldProperties { Name = Denia.CampoExtra(ação), Value = extra });

            await dm.SendMessageAsync(new MessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Title = Denia.DmTítulo(ação),
                        Description = Denia.DmDescrição(ação, guild.Name),
                        Color = new Color(ColorFor(ação)),
                        Fields = fields,
                        Timestamp = DateTimeOffset.UtcNow,
                    },
                ],
            });
        }
        
        catch (RestException)
        {
            // DM fechada — segue a punição normalmente
        }
    }

    internal static EmbedProperties BuildEmbed(
        AçãoModeração ação, User alvo, User moderador, string motivo, string? extra = null)
    {
        var avatar = (alvo.GetAvatarUrl() ?? alvo.DefaultAvatarUrl).ToString();

        List<EmbedFieldProperties> fields =
        [
            new()
            {
                Name = Denia.CampoAlvo(ação),
                Value = $"{alvo} • `{alvo.Id}`",
                Inline = true,
            },
            new()
            {
                Name = Denia.CampoModerador(ação),
                Value = $"{moderador} • `{moderador.Id}`",
                Inline = true,
            },
        ];

        if (extra is not null)
            fields.Add(new EmbedFieldProperties { Name = Denia.CampoExtra(ação), Value = extra, Inline = true });

        fields.Add(new EmbedFieldProperties { Name = Denia.CampoMotivo, Value = motivo });

        return new EmbedProperties
        {
            Author = new EmbedAuthorProperties
            {
                Name = Denia.Título(ação),
                IconUrl = avatar,
            },
            Description = Denia.Descrição(ação, alvo.ToString(), alvo.Id),
            Color = new Color(ColorFor(ação)),
            Fields = fields,
            Thumbnail = new EmbedThumbnailProperties(avatar),
            Footer = new EmbedFooterProperties { Text = Denia.Rodapé(moderador.Username) },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    internal static int ColorFor(AçãoModeração ação) => ação switch
    {
        AçãoModeração.Kick => 0xE67E22,   // laranja
        AçãoModeração.Mute => 0x9B59B6,   // roxo
        AçãoModeração.Unmute or AçãoModeração.Unban => 0x2ECC71,   // verde: aqui a notícia é boa
        _ => 0xE74C3C,                    // vermelho
    };

    /// <summary>Cargo mais alto do usuário, ou null quando ele só tem o @everyone.</summary>
    internal static Role? TopRole(Guild guild, GuildUser user)
    {
        Role? top = null;

        foreach (var roleId in user.RoleIds)
        {
            if (guild.Roles.TryGetValue(roleId, out var role) && (top is null || role.Position > top.Position))
                top = role;
        }

        return top;
    }
}

using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.Dados;

using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace AlephBot.Core.Commands.Admin;

/// <summary>
/// /config — o que este servidor escolheu pra si. Sem nenhuma opção, mostra como está;
/// com elas, muda só o que foi preenchido e deixa o resto quieto.
///
/// Pede "Gerenciar servidor" porque mexe no bot pra todo mundo que está lá dentro, não só
/// pra quem digitou.
/// </summary>
public sealed class ConfigCommand(ConfigDosServidores configs) : AlephSlashModule
{
    public override string Name => "config";
    public override string Description => "Mostra ou muda o que o bot faz neste servidor.";
    public override CommandCategory Category => CommandCategory.Moderation;
    public override string? Usage => "config [prefixo] [canal-de-log] [espera]";

    /// <summary>O que o usuário digita pra tirar uma escolha e voltar ao padrão do bot.</summary>
    private const string Limpar = "padrão";

    private const int EsperaMínima = 1;
    private const int EsperaMáxima = 180;

    [RequireContext<ApplicationCommandContext>(RequiredContext.Guild)]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageGuild)]
    [SlashCommand("config", "Mostra ou muda o que o bot faz neste servidor.")]
    public async Task ConfigAsync(
        [SlashCommandParameter(
            Name = "prefixo",
            Description = "O sinal dos comandos de texto aqui. \"padrão\" volta ao do bot.")]
        string? prefixo = null,
        [SlashCommandParameter(
            Name = "canal-de-log",
            Description = "Onde avisar as ações de moderação.")]
        TextGuildChannel? canalDeLog = null,
        [SlashCommandParameter(
            Name = "espera",
            Description = "Minutos parado até o bot sair da call (1 a 180).")]
        int? espera = null)
    {
        if (Context.Guild?.Id is not { } guildId)
        {
            await ErrorAsync(Denia.SóEmServidor());
            return;
        }

        // nada preenchido: é pergunta, não mudança
        if (prefixo is null && canalDeLog is null && espera is null)
        {
            await RespondAsync(await ComoEstáAsync(guildId));
            return;
        }

        if (espera is { } minutos && minutos is < EsperaMínima or > EsperaMáxima)
        {
            await ErrorAsync(Denia.ConfigEsperaInválida(EsperaMínima, EsperaMáxima));
            return;
        }

        if (prefixo is { } novo && !ÉPrefixoAceitável(novo))
        {
            await ErrorAsync(Denia.ConfigPrefixoInválido());
            return;
        }

        var guardou = await configs.SalvaAsync(guildId, linha =>
        {
            if (prefixo is { } valor)
                linha.Prefixo = ÉPadrão(valor) ? null : valor.Trim();

            if (canalDeLog is not null)
                linha.CanalDeLog = canalDeLog.Id;

            if (espera is { } minutos)
                linha.MinutosDeEspera = minutos;
        });

        if (!guardou)
        {
            await ErrorAsync(Denia.ConfigSemBanco());
            return;
        }

        await RespondAsync(await ComoEstáAsync(guildId, mudou: true));
    }

    internal static bool ÉPadrão(string valor) =>
        string.Equals(valor.Trim(), Limpar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prefixo com espaço no meio ou vazio faria comando nenhum ser reconhecido, e quem
    /// mudou não teria como desfazer — o comando de texto pra voltar atrás também pararia.
    /// </summary>
    internal static bool ÉPrefixoAceitável(string valor)
    {
        if (ÉPadrão(valor))
            return true;

        var limpo = valor.Trim();

        return limpo.Length is > 0 and <= 16 && !limpo.Contains(' ');
    }

    private async Task<EmbedProperties> ComoEstáAsync(ulong guildId, bool mudou = false)
    {
        var linha = await configs.DoServidorAsync(guildId);
        var prefixo = await configs.PrefixoAsync(guildId);
        var espera = await configs.EsperaDaMúsicaAsync(guildId);

        return new EmbedProperties
        {
            Title = mudou ? "Guardado" : "Como este servidor está",
            Color = new Color(mudou ? 0x57F287 : 0x5865F2),
            Fields =
            [
                Campo("Prefixo", $"`{prefixo}`", linha?.Prefixo is null),
                Campo(
                    "Canal de log",
                    linha?.CanalDeLog is { } canal ? $"<#{canal}>" : "nenhum",
                    linha?.CanalDeLog is null),
                Campo(
                    "Espera da música",
                    $"{espera.TotalMinutes:F0} min",
                    linha?.MinutosDeEspera is null),
            ],
            Footer = new EmbedFooterProperties
            {
                Text = "O que está como padrão segue o que vier no .env do bot.",
            },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static EmbedFieldProperties Campo(string nome, string valor, bool padrão) => new()
    {
        Name = nome,
        Value = padrão ? $"{valor} *(padrão)*" : valor,
        Inline = true,
    };
}

using AlephBot.Config;
using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Interaction;

/// <summary>
/// /help — a lista por categoria, e o detalhe de um comando só. Nada escrito à mão: quem sabe
/// o que existe é o <see cref="CommandRegistry"/>, então a lista se cuida sozinha.
/// </summary>
public sealed class HelpCommand : AlephSlashModule
{
    // roxo da Denia, o mesmo do mute
    private const int Roxo = 0x9B59B6;

    // teto do Discord para o valor de um campo de embed
    private const int MaxCampo = 1024;

    private readonly CommandRegistry _registry;
    private readonly AlephConfig _config;

    // o registry me instancia pra ler meus metadados e acaba pedindo ele mesmo aqui.
    // por isso só guardo a referência — ler qualquer coisa dele agora seria dar a volta
    public HelpCommand(CommandRegistry registry, AlephConfig config)
    {
        _registry = registry;
        _config = config;
    }

    public override string Name => "help";
    public override string Description => "Mostra o que eu sei fazer.";
    public override CommandCategory Category => CommandCategory.General;
    public override string? Usage => "help [comando]";

    [SlashCommand("help", "Mostra o que eu sei fazer.")]
    public Task HelpAsync(
        [SlashCommandParameter(Name = "comando", Description = "Qual comando você quer entender.")] string? comando = null)
    {
        if (string.IsNullOrWhiteSpace(comando))
            return RespondAsync(BuildList(_registry, _config.Prefix));

        return BuildDetail(_registry, _config.Prefix, comando) is { } embed
            ? RespondAsync(embed)
            : ErrorAsync(Denia.HelpNãoAchei(comando.Trim()));
    }

    /// <summary>A lista inteira: uma categoria por campo, um comando por linha.</summary>
    internal static EmbedProperties BuildList(CommandRegistry registry, string prefixo)
    {
        var categorias = registry.ByCategory.ToList();
        var total = registry.Visible.Count();

        return new EmbedProperties
        {
            Title = Denia.HelpTítulo,
            Description = categorias.Count == 0
                ? Denia.HelpVazio()
                : $"{Denia.HelpIntro(total, prefixo)}\n{Denia.HelpDica()}",
            Color = new Color(Roxo),
            Fields = [.. categorias.Select(Campo)],
            Footer = new EmbedFooterProperties { Text = Denia.HelpRodapé() },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>O detalhe de um comando, ou null quando não existe nenhum com esse nome.</summary>
    internal static EmbedProperties? BuildDetail(CommandRegistry registry, string prefixo, string comando)
    {
        var busca = Normaliza(comando, prefixo);

        // slash e prefixo são classes separadas com o mesmo Name; pego as duas, e é
        // por isso que o "Como chamar" mostra as duas formas juntas
        var entradas = registry.All
            .Where(c => string.Equals(c.Name, busca, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entradas.Count == 0)
            return null;

        // a versão visível é a que descreve o comando; a escondida é só o gêmeo de prefixo
        var principal = entradas.FirstOrDefault(c => !c.IsHidden) ?? entradas[0];

        var usos = entradas
            .Select(c => c.Uso(prefixo))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var atalhos = entradas
            .SelectMany(c => CommandAudit.AliasesDeTexto(c.Type))
            .Where(a => !string.Equals(a, principal.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<EmbedFieldProperties> campos =
        [
            new()
            {
                // Uso nunca vem vazio (no pior caso cai no nome), então não trato ausência
                Name = Denia.HelpCampoUso,
                Value = Recorta(string.Join('\n', usos.Select(u => $"`{u}`"))),
            },
            new()
            {
                Name = Denia.HelpCampoCategoria,
                Value = Denia.HelpCategoria(principal.Category),
                Inline = true,
            },
        ];

        if (atalhos.Count > 0)
        {
            campos.Add(new EmbedFieldProperties
            {
                Name = Denia.HelpCampoAtalhos,
                Value = Recorta(string.Join(" ", atalhos.Select(a => $"`{prefixo}{a}`"))),
                Inline = true,
            });
        }

        return new EmbedProperties
        {
            Title = Denia.HelpDetalheTítulo($"/{principal.Name}"),
            Description = principal.Description,
            Color = new Color(Roxo),
            Fields = campos,
            Footer = new EmbedFooterProperties { Text = Denia.Assinatura },
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static EmbedFieldProperties Campo(IGrouping<CommandCategory, CommandInfo> categoria) =>
        new()
        {
            Name = Denia.HelpCategoria(categoria.Key),
            Value = Recorta(string.Join('\n', categoria.OrderBy(c => c.Name).Select(Linha))),
        };

    private static string Linha(CommandInfo comando) =>
        $"`/{comando.Name}` — {comando.Description}";

    /// <summary>Aceita "mute", "/mute" e "!mute" — quem pede ajuda costuma colar o comando inteiro.</summary>
    internal static string Normaliza(string comando, string prefixo)
    {
        var limpo = comando.Trim();

        // prefixo configurado pode ser uma palavra ("bot "), então sai primeiro
        if (limpo.StartsWith(prefixo, StringComparison.Ordinal))
            limpo = limpo[prefixo.Length..];

        // nome de comando não começa com sinal — o que vier antes da primeira letra
        // é enfeite, nem que seja o prefixo de outro bot
        var início = 0;

        while (início < limpo.Length && !char.IsLetterOrDigit(limpo[início]))
            início++;

        return limpo[início..].Trim();
    }

    /// <summary>Corta no fim da última linha inteira: campo estourado o Discord recusa.</summary>
    private static string Recorta(string texto)
    {
        if (texto.Length <= MaxCampo)
            return texto;

        var corte = texto.LastIndexOf('\n', MaxCampo - 2);

        return corte > 0 ? $"{texto[..corte]}\n…" : $"{texto[..(MaxCampo - 1)]}…";
    }
}

/// <summary>!help — mesma lista do /help, para quem prefere o prefixo.</summary>
public sealed class HelpTextCommand : AlephTextModule
{
    private readonly CommandRegistry _registry;
    private readonly AlephConfig _config;

    public HelpTextCommand(CommandRegistry registry, AlephConfig config)
    {
        _registry = registry;
        _config = config;
    }

    public override string Name => "help";
    public override string Description => "Mostra o que eu sei fazer.";
    public override CommandCategory Category => CommandCategory.General;
    public override string? Usage => "help [comando]";

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [Command("help", "ajuda", "comandos")]
    public Task HelpAsync([CommandParameter(Remainder = true)] string? comando = null)
    {
        if (string.IsNullOrWhiteSpace(comando))
            return ReplyAsync(HelpCommand.BuildList(_registry, _config.Prefix));

        return HelpCommand.BuildDetail(_registry, _config.Prefix, comando) is { } embed
            ? ReplyAsync(embed)
            : ErrorAsync(Denia.HelpNãoAchei(comando.Trim()));
    }
}

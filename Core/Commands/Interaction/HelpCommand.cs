using AlephBot.Config;
using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;

using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;
using NetCord.Services.ComponentInteractions;

namespace AlephBot.Core.Commands.Interaction;

// /help — a capa com o menu de categorias, ou o detalhe de um comando.
public sealed class HelpCommand : AlephSlashModule
{
    private readonly CommandRegistry _registry;
    private readonly AlephConfig _config;

    // o registry me instancia pra ler meus metadados, então só guardo a referência
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
        // efêmero: lista de ajuda é ruído no canal, e assim o menu é só de quem pediu
        if (string.IsNullOrWhiteSpace(comando))
        {
            return RespondAsync(
                Help.Capa(_registry.Visible, _config.Prefix),
                [Help.Menu(_registry.Visible, Help.MenuId)],
                ephemeral: true);
        }

        return Help.Detalhe(_registry.All, _config.Prefix, comando) is { } embed
            ? RespondAsync(embed, [Help.Menu(_registry.Visible, Help.MenuId)], ephemeral: true)
            : ErrorAsync(Denia.HelpNãoAchei(comando.Trim()));
    }
}

// !help — a mesma coisa, para quem prefere o prefixo.
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
        // o menu daqui é o público: esta mensagem fica no canal, visível pra todo mundo
        if (string.IsNullOrWhiteSpace(comando))
        {
            return ReplyAsync(
                Help.Capa(_registry.Visible, _config.Prefix),
                [Help.Menu(_registry.Visible, Help.MenuIdPúblico)]);
        }

        return Help.Detalhe(_registry.All, _config.Prefix, comando) is { } embed
            ? ReplyAsync(embed, [Help.Menu(_registry.Visible, Help.MenuIdPúblico)])
            : ErrorAsync(Denia.HelpNãoAchei(comando.Trim()));
    }
}

// O menu do /help. Sem estado: a categoria viaja no valor escolhido, então o menu de ontem
// continua funcionando depois de um restart.
public sealed class HelpNavegaçãoModule : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly CommandRegistry _registry;
    private readonly AlephConfig _config;

    public HelpNavegaçãoModule(CommandRegistry registry, AlephConfig config)
    {
        _registry = registry;
        _config = config;
    }

    // A mensagem do /help é efêmera: só quem pediu enxerga, então troco a página no lugar.
    [ComponentInteraction(Help.MenuId)]
    public Task NavegarAsync()
    {
        var (embed, selecionada) = Página();

        return Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(mensagem =>
        {
            mensagem.Embeds = [embed];
            mensagem.Components = [Help.Menu(_registry.Visible, Help.MenuId, selecionada)];
        }));
    }

    // A do !help é pública: editar seria reescrever a mensagem de outro na frente do canal,
    // então quem clica recebe a página só pra si.
    [ComponentInteraction(Help.MenuIdPúblico)]
    public Task NavegarPúblicoAsync()
    {
        var (embed, selecionada) = Página();

        return Context.Interaction.SendResponseAsync(InteractionCallback.Message(
            new InteractionMessageProperties
            {
                Embeds = [embed],
                Components = [Help.Menu(_registry.Visible, Help.MenuIdPúblico, selecionada)],
                Flags = MessageFlags.Ephemeral,
            }));
    }

    // Valor que não existe mais cai na capa: o menu antigo não deve explodir na mão de ninguém.
    private (EmbedProperties Embed, CommandCategory? Selecionada) Página()
    {
        var escolha = Context.SelectedValues.FirstOrDefault();

        if (escolha is null or Help.ValorDaCapa)
            return (Help.Capa(_registry.Visible, _config.Prefix), null);

        return Help.Categoria(_registry.Visible, escolha) is { } embed
               && Enum.TryParse<CommandCategory>(escolha, ignoreCase: true, out var categoria)
            ? (embed, categoria)
            : (Help.Capa(_registry.Visible, _config.Prefix), null);
    }
}

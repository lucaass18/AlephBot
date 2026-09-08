using AlephBot.Core.Commands.Interface;

using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace AlephBot.Core.Commands.Abstractions;

/// <summary>
/// Base para comandos de barra (/comando).
/// Classes abstratas são ignoradas pelo AddModules, então isso não vira comando.
/// </summary>
public abstract class AlephSlashModule : ApplicationCommandModule<ApplicationCommandContext>, ICommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public virtual CommandCategory Category => CommandCategory.General;
    public virtual string? Usage => null;
    public virtual bool IsHidden => false;

    // ---- helpers de resposta -------------------------------------------------

    
    protected Task RespondAsync(string content, bool ephemeral = false) =>
        Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = content,
                Flags = ephemeral ? MessageFlags.Ephemeral : null,
            }));

    
    protected Task RespondAsync(EmbedProperties embed, bool ephemeral = false) =>
        Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds = [embed],
                Flags = ephemeral ? MessageFlags.Ephemeral : null,
            }));

    
    protected Task DeferAsync(bool ephemeral = false) =>
        Context.Interaction.SendResponseAsync(
            InteractionCallback.DeferredMessage(
                ephemeral ? MessageFlags.Ephemeral : null));

    
    protected Task FollowupAsync(string content) =>
        Context.Interaction.SendFollowupMessageAsync(content);

    protected Task ErrorAsync(string message) =>
        RespondAsync(new EmbedProperties
        {
            Description = $"❌ {message}",
            Color = new Color(0xE74C3C),
        }, ephemeral: true);

    /// <summary>
    /// Fecha um <see cref="DeferAsync"/>. O "pensando..." já é mensagem publicada e tem que
    /// ser editada: followup criaria uma segunda e deixaria a primeira carregando pra sempre.
    /// </summary>
    protected Task<RestMessage> EditarRespostaAsync(EmbedProperties embed) =>
        Context.Interaction.ModifyResponseAsync(mensagem => mensagem.Embeds = [embed]);

    /// <summary>
    /// O ErrorAsync de quem já adiou. Sai visível pra todo mundo, e não escondido como o
    /// ErrorAsync: a visibilidade da resposta foi decidida no DeferAsync e não dá pra mudar.
    /// </summary>
    protected Task<RestMessage> EditarComErroAsync(string message) =>
        EditarRespostaAsync(new EmbedProperties
        {
            Description = $"❌ {message}",
            Color = new Color(0xE74C3C),
        });

    protected Task SuccessAsync(string message) =>
        RespondAsync(new EmbedProperties
        {
            Description = $"✅ {message}",
            Color = new Color(0x2ECC71),
        });
}
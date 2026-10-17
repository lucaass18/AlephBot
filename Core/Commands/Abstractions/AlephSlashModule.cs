using AlephBot.Core.Commands.Interface;

using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace AlephBot.Core.Commands.Abstractions;

// Base dos comandos de barra. Classe abstrata o AddModules ignora, então não vira comando.
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

    
    // Embed com componentes: quem tem menu (o /help) responde por aqui.
    protected Task RespondAsync(
        EmbedProperties embed,
        IEnumerable<IMessageComponentProperties> componentes,
        bool ephemeral = false) =>
        Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds = [embed],
                Components = componentes,
                Flags = ephemeral ? MessageFlags.Ephemeral : null,
            }));

    // A resposta no formato novo (container com capa grande); ele e embed não convivem.
    protected Task RespondAsync(ComponentContainerProperties cartão, AttachmentProperties? anexo = null) =>
        Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(new InteractionMessageProperties
            {
                Components = [cartão],
                Attachments = anexo is null ? null : [anexo],
                Flags = MessageFlags.IsComponentsV2,
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

    // Fecha um DeferAsync: o "pensando..." já é mensagem e tem que ser editada, não seguida.
    protected Task<RestMessage> EditarRespostaAsync(EmbedProperties embed) =>
        Context.Interaction.ModifyResponseAsync(mensagem => mensagem.Embeds = [embed]);

    // Embed com arquivo junto: é assim que o /ma manda a ficha desenhada, que o embed
    // referencia por attachment://. Sem anexo, cai no fecho de sempre.
    protected Task<RestMessage> EditarRespostaAsync(EmbedProperties embed, AttachmentProperties? anexo) =>
        anexo is null
            ? EditarRespostaAsync(embed)
            : Context.Interaction.ModifyResponseAsync(mensagem =>
            {
                mensagem.Embeds = [embed];
                mensagem.Attachments = [anexo];
            });

    // O mesmo fecho, no formato novo. A flag vai aqui porque o "pensando..." nasceu sem ela,
    // e sem ela o Discord recusa o container.
    protected Task<RestMessage> EditarRespostaAsync(
        ComponentContainerProperties cartão, AttachmentProperties? anexo = null) =>
        Context.Interaction.ModifyResponseAsync(mensagem =>
        {
            mensagem.Components = [cartão];
            mensagem.Attachments = anexo is null ? null : [anexo];
            mensagem.Flags = MessageFlags.IsComponentsV2;
        });

    // O ErrorAsync de quem já adiou: sai visível, porque isso foi decidido no DeferAsync.
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
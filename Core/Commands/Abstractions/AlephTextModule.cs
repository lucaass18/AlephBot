using AlephBot.Core.Commands.Contratos;

using NetCord;
using NetCord.Rest;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Abstractions;

// Base para comandos com prefixo (!comando).
public abstract class AlephTextModule : CommandModule<CommandContext>, ICommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public virtual CommandCategory Category => CommandCategory.General;
    public virtual string? Usage => null;
    public virtual bool IsHidden => false;

    protected Task<RestMessage> ReplyAsync(string content) =>
        Context.Message.ReplyAsync(content);

    protected Task<RestMessage> ReplyAsync(EmbedProperties embed) =>
        Context.Message.ReplyAsync(new ReplyMessageProperties { Embeds = [embed] });

    // O mesmo embed, com componentes. Ver o módulo de barra.
    protected Task<RestMessage> ReplyAsync(
        EmbedProperties embed, IEnumerable<IMessageComponentProperties> componentes) =>
        Context.Message.ReplyAsync(new ReplyMessageProperties
        {
            Embeds = [embed],
            Components = componentes,
        });

    // A resposta no formato novo. Ver o módulo de barra.
    protected Task<RestMessage> ReplyAsync(ComponentContainerProperties cartão, AttachmentProperties? anexo = null) =>
        Context.Message.ReplyAsync(new ReplyMessageProperties
        {
            Components = [cartão],
            Attachments = anexo is null ? null : [anexo],
            Flags = MessageFlags.IsComponentsV2,
        });

    protected Task<RestMessage> ErrorAsync(string message) =>
        ReplyAsync(new EmbedProperties
        {
            Description = $"❌ {message}",
            Color = new Color(0xE74C3C),
        });
}
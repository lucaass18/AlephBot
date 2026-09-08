using AlephBot.Core.Commands.Interface;

using NetCord;
using NetCord.Rest;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Abstractions;

/// <summary>Base para comandos com prefixo (!comando).</summary>
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

    protected Task<RestMessage> ErrorAsync(string message) =>
        ReplyAsync(new EmbedProperties
        {
            Description = $"❌ {message}",
            Color = new Color(0xE74C3C),
        });
}
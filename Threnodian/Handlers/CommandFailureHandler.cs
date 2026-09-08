using AlephBot.Config;
using AlephBot.Core.Commands;
using AlephBot.Core.Personality;

using Microsoft.Extensions.Logging;

using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Services.Commands;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.Commands;

namespace AlephBot.Threnodian.Handlers;

/// <summary>
/// A NetCord responde as falhas dos comandos com prefixo em inglês ("Too few parameters
/// provided."). Aqui elas viram mensagens em português, com o Usage do comando junto.
/// </summary>
public sealed class CommandFailureHandler : ICommandResultHandler<CommandContext>
{
    private readonly CommandRegistry _registry;
    private readonly AlephConfig _config;

    public CommandFailureHandler(CommandRegistry registry, AlephConfig config)
    {
        _registry = registry;
        _config = config;
    }

    public async ValueTask HandleResultAsync(
        IExecutionResult result,
        CommandContext context,
        GatewayClient client,
        ILogger logger,
        IServiceProvider services)
    {
        if (result is not IFailResult fail)
            return;

        // "!" seguido de qualquer coisa que não é comando: fica quieto
        if (result is NotFoundResult)
            return;

        if (result is IExceptionResult { Exception: var exception })
            logger.LogError(exception, "Falha executando '{Content}'", context.Message.Content);

        await context.Message.ReplyAsync(new ReplyMessageProperties
        {
            Embeds =
            [
                new EmbedProperties
                {
                    Description = $"❌ {Translate(fail, context)}",
                    Color = new Color(0xE74C3C),
                },
            ],
        });
    }

    private string Translate(IFailResult fail, CommandContext context) => fail switch
    {
        ParameterCountMismatchResult { Type: ParameterCountMismatchType.TooFew } =>
            Denia.FaltouArgumento(Usage(context)),

        ParameterCountMismatchResult =>
            Denia.SobrouArgumento(Usage(context)),

        CommandTypeReaderFailResult or CommandTypeParserFailResult =>
            Denia.NãoEntendi(Usage(context)),

        MissingPermissionsResult { EntityType: MissingPermissionsResultEntityType.Bot } =>
            Denia.SemPermissãoDoBot(),

        MissingPermissionsResult =>
            Denia.SemPermissãoDoUsuário(),

        InvalidContextResult =>
            Denia.SóEmServidor(),

        IExceptionResult =>
            Denia.ErroInterno(),

        _ => fail.Message,
    };

    /// <summary>Usage do comando digitado, caindo pro nome cru quando não acho no registry.</summary>
    private string Usage(CommandContext context)
    {
        var conteúdo = context.Message.Content.AsSpan();

        if (conteúdo.Length < _config.Prefix.Length)
            return _config.Prefix;

        conteúdo = conteúdo[_config.Prefix.Length..].TrimStart();

        var fim = conteúdo.IndexOf(' ');
        var nome = (fim < 0 ? conteúdo : conteúdo[..fim]).ToString();

        // o mesmo nome existe no slash e no prefixo — a falha veio da versão com prefixo,
        // então é o uso dela que ajuda quem digitou errado
        var info = _registry.All.FirstOrDefault(c =>
            c.IsTexto && string.Equals(c.Name, nome, StringComparison.OrdinalIgnoreCase));

        return info?.Uso(_config.Prefix) ?? $"{_config.Prefix}{nome}";
    }
}

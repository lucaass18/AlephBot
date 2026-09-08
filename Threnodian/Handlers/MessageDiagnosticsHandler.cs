using Microsoft.Extensions.Logging;

using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace AlephBot.Threnodian.Handlers;

/// <summary>
/// Diagnóstico temporário: loga toda mensagem que chega pelo gateway. Se o Content vier
/// vazio, a intent de MessageContent está desligada no Developer Portal. Pode apagar
/// quando os comandos com prefixo estiverem de pé.
/// </summary>
public sealed class MessageDiagnosticsHandler : IMessageCreateGatewayHandler
{
    private readonly ILogger<MessageDiagnosticsHandler> _logger;

    public MessageDiagnosticsHandler(ILogger<MessageDiagnosticsHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(Message message)
    {
        if (message.Author.IsBot)
            return default;

        _logger.LogInformation(
            "MessageCreate | autor: {Author} | tamanho do conteúdo: {Length} | conteúdo: '{Content}'",
            message.Author.Username,
            message.Content.Length,
            message.Content);

        return default;
    }
}

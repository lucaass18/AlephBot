using AlephBot.Config;
using AlephBot.Core.Commands;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Diagnostics;

/// <summary>
/// No boot, lista o que foi descoberto por pasta e avisa sobre comando que nunca vai
/// responder. Comando novo entra sozinho — isto só te conta quando ele entra quebrado.
/// </summary>
public sealed class CommandAuditService : IHostedService
{
    private readonly CommandRegistry _registry;
    private readonly AlephConfig _config;
    private readonly ILogger<CommandAuditService> _logger;

    public CommandAuditService(CommandRegistry registry, AlephConfig config, ILogger<CommandAuditService> logger)
    {
        _registry = registry;
        _config = config;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var comandos = _registry.All;

        foreach (var pasta in comandos.GroupBy(c => c.Pasta).OrderBy(g => g.Key))
        {
            _logger.LogInformation(
                "Comandos em {Pasta}: {Lista}",
                pasta.Key,
                string.Join(", ", pasta.Select(c => c.Uso(_config.Prefix))));
        }

        var problemas = CommandAudit.Run(comandos);

        foreach (var problema in problemas)
            _logger.LogWarning("Comando '{Command}': {Problem}", problema.Command, problema.Message);

        if (problemas.Count == 0)
            _logger.LogInformation("{Count} comandos conferidos, nenhum problema", comandos.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

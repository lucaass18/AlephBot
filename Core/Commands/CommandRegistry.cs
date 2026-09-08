using System.Reflection;

using AlephBot.Core.Commands.Interface;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlephBot.Core.Commands;

/// <summary>
/// Descobre, por reflection, todo mundo que implementa ICommand no assembly.
/// Comando novo em qualquer pasta entra aqui sem precisar registrar nada à mão.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Lazy<IReadOnlyList<CommandInfo>> _commands;

    public CommandRegistry(IServiceProvider services, ILogger<CommandRegistry> logger)
    {
        // lazy de propósito: ver a nota sobre dependência circular abaixo
        _commands = new Lazy<IReadOnlyList<CommandInfo>>(() => Discover(services, logger));
    }

    public IReadOnlyList<CommandInfo> All => _commands.Value;

    public IEnumerable<CommandInfo> Visible => All.Where(c => !c.IsHidden);

    public IEnumerable<IGrouping<CommandCategory, CommandInfo>> ByCategory =>
        Visible.GroupBy(c => c.Category).OrderBy(g => g.Key);

    public CommandInfo? Find(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    private static List<CommandInfo> Discover(IServiceProvider services, ILogger logger)
    {
        var found = new List<CommandInfo>();

        var types = typeof(CommandRegistry).Assembly
            .GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in types)
        {
            try
            {
                // CreateInstance resolve as dependências do construtor via DI
                var command = (ICommand)ActivatorUtilities.CreateInstance(services, type);

                found.Add(new CommandInfo(
                    command.Name,
                    command.Description,
                    command.Category,
                    command.Usage,
                    command.IsHidden,
                    type));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Não consegui ler os metadados de {Command}", type.FullName);
            }
        }

        logger.LogInformation("{Count} comandos descobertos", found.Count);

        return found.OrderBy(c => c.Name).ToList();
    }
}
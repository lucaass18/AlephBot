using System.Reflection;

using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands;

/// <summary>Um problema encontrado num comando — some sozinho quando a classe é corrigida.</summary>
public sealed record CommandProblem(string Command, string Message);

/// <summary>
/// O <see cref="CommandRegistry"/> acha qualquer ICommand, mas achar não é funcionar: sem
/// [SlashCommand]/[Command] a classe entra no /help e nunca é chamada, e dois comandos podem
/// brigar pelo mesmo nome sem ninguém notar. Isto roda no boot e vira aviso no log.
/// </summary>
public static class CommandAudit
{
    public static IReadOnlyList<CommandProblem> Run(IReadOnlyList<CommandInfo> comandos)
    {
        var problemas = new List<CommandProblem>();

        foreach (var comando in comandos)
        {
            if (!TemGatilho(comando.Type))
            {
                problemas.Add(new CommandProblem(
                    comando.Name,
                    $"{comando.Type.Name} não tem [SlashCommand] nem [Command] — aparece no /help mas nunca vai ser chamado."));

                continue;
            }

            // /help promete um nome; o Discord registra outro
            foreach (var slash in NomesDeSlash(comando.Type))
            {
                if (!string.Equals(slash, comando.Name, StringComparison.OrdinalIgnoreCase))
                {
                    problemas.Add(new CommandProblem(
                        comando.Name,
                        $"{comando.Type.Name}: o /help mostra '{comando.Name}' mas o slash registrado é '/{slash}'."));
                }
            }
        }

        problemas.AddRange(Duplicados(comandos, AliasesDeTexto, "prefixo"));
        problemas.AddRange(Duplicados(comandos, c => NomesDeSlash(c), "slash"));

        return problemas;
    }

    /// <summary>Dois gatilhos iguais em classes diferentes: um deles nunca roda.</summary>
    private static IEnumerable<CommandProblem> Duplicados(
        IReadOnlyList<CommandInfo> comandos, Func<Type, IEnumerable<string>> gatilhos, string tipo)
    {
        var vistos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var comando in comandos)
        {
            foreach (var gatilho in gatilhos(comando.Type))
            {
                if (vistos.TryGetValue(gatilho, out var dono) && dono != comando.Type.Name)
                {
                    yield return new CommandProblem(
                        comando.Name,
                        $"'{gatilho}' ({tipo}) está em {dono} e em {comando.Type.Name} — só um dos dois responde.");
                }
                else
                {
                    vistos[gatilho] = comando.Type.Name;
                }
            }
        }
    }

    private static bool TemGatilho(Type tipo) =>
        Tem<CommandAttribute>(tipo)
        || Tem<SlashCommandAttribute>(tipo)
        || Tem<SubSlashCommandAttribute>(tipo)
        || Tem<UserCommandAttribute>(tipo)
        || Tem<MessageCommandAttribute>(tipo);

    private static bool Tem<TAttribute>(Type tipo) where TAttribute : Attribute =>
        tipo.GetCustomAttribute<TAttribute>() is not null
        || Metodos(tipo).Any(m => m.GetCustomAttribute<TAttribute>() is not null);

    /// <summary>Todos os nomes que chamam a versão com prefixo, o oficial junto. O /help também usa.</summary>
    internal static IEnumerable<string> AliasesDeTexto(Type tipo) =>
        Atributos<CommandAttribute>(tipo).SelectMany(a => a.Aliases);

    private static IEnumerable<string> NomesDeSlash(Type tipo) =>
        Atributos<SlashCommandAttribute>(tipo).Select(a => a.Name);

    private static IEnumerable<TAttribute> Atributos<TAttribute>(Type tipo) where TAttribute : Attribute =>
        tipo.GetCustomAttributes<TAttribute>()
            .Concat(Metodos(tipo).SelectMany(m => m.GetCustomAttributes<TAttribute>()));

    private static IEnumerable<MethodInfo> Metodos(Type tipo) =>
        tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
}

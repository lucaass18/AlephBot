using System.Runtime.CompilerServices;

using AlephBot.Core.Commands;
using AlephBot.Core.Commands.Interface;

using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Tests.Commands;

public class CommandAuditTests
{
    private static CommandInfo Info<T>(string nome) =>
        new(nome, "descrição", CommandCategory.General, null, false, typeof(T));

    [Fact]
    public void Comando_certo_não_gera_problema()
    {
        Assert.Empty(CommandAudit.Run([Info<ComandoCerto>("certo")]));
    }

    [Fact]
    public void Classe_sem_gatilho_nunca_seria_chamada()
    {
        var problema = Assert.Single(CommandAudit.Run([Info<ComandoSemGatilho>("mudo")]));

        Assert.Equal("mudo", problema.Command);
        Assert.Contains("nem [Command]", problema.Message);
    }

    [Fact]
    public void Nome_do_help_diferente_do_slash_registrado()
    {
        var problema = Assert.Single(CommandAudit.Run([Info<ComandoComNomeTrocado>("certo")]));

        Assert.Contains("'/outro'", problema.Message);
    }

    [Fact]
    public void Atalho_de_prefixo_repetido_em_duas_classes()
    {
        var problema = Assert.Single(CommandAudit.Run([Info<TextoA>("a"), Info<TextoB>("b")]));

        Assert.Contains("'repetido' (prefixo)", problema.Message);
    }

    [Fact]
    public void Slash_repetido_em_duas_classes()
    {
        var problemas = CommandAudit.Run([Info<SlashA>("igual"), Info<SlashB>("igual")]);

        Assert.Contains(problemas, p => p.Message.Contains("'igual' (slash)"));
    }

    // ---- os comandos de verdade ----------------------------------------------

    /// <summary>
    /// Os comandos do bot, lidos sem DI: nome, descrição e categoria são constantes, então a
    /// instância nem precisa do construtor (que pediria Lavalink, Discord e companhia).
    /// </summary>
    private static List<CommandInfo> ComandosDoBot() =>
        typeof(CommandRegistry).Assembly.GetTypes()
            .Where(tipo => typeof(ICommand).IsAssignableFrom(tipo)
                           && tipo is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Select(tipo => (ICommand)RuntimeHelpers.GetUninitializedObject(tipo))
            .Select(c => new CommandInfo(c.Name, c.Description, c.Category, c.Usage, c.IsHidden, c.GetType()))
            .ToList();

    [Fact]
    public void Os_comandos_do_bot_passam_na_auditoria()
    {
        var comandos = ComandosDoBot();

        Assert.NotEmpty(comandos);
        Assert.Empty(CommandAudit.Run(comandos));
    }

    [Fact]
    public void Todo_comando_visível_tem_descrição()
    {
        Assert.All(
            ComandosDoBot().Where(c => !c.IsHidden),
            c => Assert.False(string.IsNullOrWhiteSpace(c.Description), $"{c.Type.Name} está sem descrição"));
    }

    [Fact]
    public void Dois_comandos_visíveis_não_disputam_o_mesmo_nome_no_help()
    {
        var repetidos = ComandosDoBot()
            .Where(c => !c.IsHidden)
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        Assert.Empty(repetidos);
    }
}

// ---- comandos de mentira ------------------------------------------------------
//
// a auditoria só olha os atributos, então não precisam ser módulos de verdade

public sealed class ComandoSemGatilho
{
    public Task ExecutaAsync() => Task.CompletedTask;
}

public sealed class ComandoCerto
{
    [SlashCommand("certo", "faz certo")]
    public Task ExecutaAsync() => Task.CompletedTask;
}

public sealed class ComandoComNomeTrocado
{
    [SlashCommand("outro", "o /help promete outro nome")]
    public Task ExecutaAsync() => Task.CompletedTask;
}

public sealed class TextoA
{
    [Command("a", "repetido")]
    public Task ExecutaAsync() => Task.CompletedTask;
}

public sealed class TextoB
{
    [Command("b", "repetido")]
    public Task ExecutaAsync() => Task.CompletedTask;
}

public sealed class SlashA
{
    [SlashCommand("igual", "um")]
    public Task ExecutaAsync() => Task.CompletedTask;
}

public sealed class SlashB
{
    [SlashCommand("igual", "outro")]
    public Task ExecutaAsync() => Task.CompletedTask;
}

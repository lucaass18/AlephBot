using AlephBot.Core.Commands;
using AlephBot.Core.Commands.Interaction;
using AlephBot.Core.Commands.Interface;

namespace AlephBot.Tests.Commands;

public class CommandInfoTests
{
    private static CommandInfo Info(Type tipo, string? uso = "ping") =>
        new("ping", "Mostra a latência do bot.", CommandCategory.Utility, uso, false, tipo);

    [Fact]
    public void Slash_usa_barra()
    {
        var info = Info(typeof(PingCommand));

        Assert.False(info.IsTexto);
        Assert.Equal("/ping", info.Uso("!"));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("?")]
    public void Prefixo_vem_da_config(string prefixo)
    {
        var info = Info(typeof(PingTextCommand));

        Assert.True(info.IsTexto);
        Assert.Equal($"{prefixo}ping", info.Uso(prefixo));
    }

    [Fact]
    public void Sem_Usage_mostra_o_nome()
    {
        Assert.Equal("/ping", Info(typeof(PingCommand), uso: null).Uso("!"));
    }

    [Fact]
    public void Pasta_é_o_fim_do_namespace()
    {
        Assert.Equal("Interaction", Info(typeof(PingCommand)).Pasta);
    }
}

using AlephBot.Core.Commands.Registro;
using AlephBot.Core.Commands;
using AlephBot.Core.Commands.Interaction;
using AlephBot.Core.Commands.Contratos;

namespace AlephBot.Tests.Commands;

public class HelpTests
{
    private static CommandInfo Info(string nome, CommandCategory categoria) =>
        new(nome, $"faz {nome}.", categoria, nome, false, typeof(PingCommand));

    // música de propósito grande: era a categoria que fazia a capa virar paredão
    private static readonly List<CommandInfo> Comandos =
    [
        Info("ban", CommandCategory.Moderation),
        Info("kick", CommandCategory.Moderation),
        Info("ping", CommandCategory.Utility),
        .. Enumerable.Range(1, 8).Select(i => Info($"som{i}", CommandCategory.Music)),
    ];

    [Fact]
    public void Capa_conta_as_categorias_sem_listar_tudo()
    {
        var capa = Help.Capa(Comandos, "!");

        Assert.Contains("**🎧 Música** · 8", capa.Description);
        Assert.Contains("+3", capa.Description);
        Assert.DoesNotContain("som8", capa.Description);
    }

    [Fact]
    public void Capa_sem_comando_nenhum_não_fica_vazia()
    {
        Assert.False(string.IsNullOrWhiteSpace(Help.Capa([], "!").Description));
    }

    [Fact]
    public void Página_lista_a_categoria_inteira()
    {
        var página = Help.Categoria(Comandos, nameof(CommandCategory.Music));

        Assert.NotNull(página);
        Assert.Contains("`/som8` — faz som8.", página.Description);
        Assert.DoesNotContain("/ping", página.Description);
    }

    [Fact]
    public void Página_de_categoria_vazia_não_existe()
    {
        Assert.Null(Help.Categoria(Comandos, nameof(CommandCategory.Owner)));
    }

    // menu de uma versão antiga do bot ainda clica: o módulo trata o null caindo na capa
    [Fact]
    public void Valor_que_não_é_categoria_não_vira_página()
    {
        Assert.Null(Help.Categoria(Comandos, "Inventada"));
    }

    [Fact]
    public void Menu_abre_com_o_índice_e_uma_opção_por_categoria()
    {
        var menu = Help.Menu(Comandos, Help.MenuId);

        Assert.Equal(4, menu.Options.Count());
        Assert.Equal(Help.ValorDaCapa, menu.Options.First().Value);
    }

    [Fact]
    public void Menu_marca_a_categoria_aberta()
    {
        var menu = Help.Menu(Comandos, Help.MenuId, CommandCategory.Music);

        Assert.Single(menu.Options, o => o.Default && o.Value == nameof(CommandCategory.Music));
    }

    [Fact]
    public void Escolher_uma_categoria_no_menu_abre_a_página_dela()
    {
        var (embed, selecionada) = Help.Página(Comandos, "!", nameof(CommandCategory.Music));

        Assert.Equal(CommandCategory.Music, selecionada);
        Assert.Contains("som8", embed.Description);
    }

    [Fact]
    public void Voltar_pro_índice_traz_a_capa_sem_nada_marcado()
    {
        var (embed, selecionada) = Help.Página(Comandos, "!", Help.ValorDaCapa);

        Assert.Null(selecionada);
        Assert.Equal(Help.Capa(Comandos, "!").Title, embed.Title);
    }

    // menu de uma versão antiga do bot ainda clica, e clique nenhum pode virar erro na cara
    [Theory]
    [InlineData(null)]
    [InlineData("Inventada")]
    public void Valor_que_não_entendo_cai_na_capa(string? escolha)
    {
        var (embed, selecionada) = Help.Página(Comandos, "!", escolha);

        Assert.Null(selecionada);
        Assert.Equal(Help.Capa(Comandos, "!").Title, embed.Title);
    }

    [Theory]
    [InlineData("mute")]
    [InlineData("/mute")]
    [InlineData("!mute")]
    public void Normaliza_aceita_o_comando_colado(string digitado)
    {
        Assert.Equal("mute", Help.Normaliza(digitado, "!"));
    }
}

using AlephBot.Core.Commands;

namespace AlephBot.Tests.Commands;

public class MarkdownTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nada_vira_vazio(string? texto)
    {
        Assert.Equal("", Markdown.Escapa(texto));
    }

    [Fact]
    public void Texto_comum_passa_como_veio()
    {
        Assert.Equal("Never Gonna Give You Up", Markdown.Escapa("Never Gonna Give You Up"));
    }

    [Fact]
    public void Colchete_de_título_não_quebra_o_embed()
    {
        Assert.Equal(@"\[Oshi no Ko\]", Markdown.Escapa("[Oshi no Ko]"));
    }

    [Theory]
    [InlineData('[')]
    [InlineData(']')]
    [InlineData('(')]
    [InlineData(')')]
    [InlineData('*')]
    [InlineData('_')]
    [InlineData('~')]
    [InlineData('`')]
    [InlineData('\\')]
    [InlineData('|')]
    [InlineData('>')]
    public void Cada_caractere_do_markdown_ganha_barra(char especial)
    {
        Assert.Equal($"a\\{especial}b", Markdown.Escapa($"a{especial}b"));
    }
}

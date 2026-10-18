using System.Reflection;

using AlephBot.Core.Commands.Registro;
using AlephBot.Core.Commands;
using AlephBot.Core.Commands.Anime;
using AlephBot.Core.Commands.Manga;
using AlephBot.Core.Personality;

using NetCord.Services.ApplicationCommands;

namespace AlephBot.Tests.MyAnimeList;

// Mangá saiu do /ma e virou o /mg; aqui ficam o gatilho de cada um e o hábito antigo.
public class MangadexCommandTests
{
    [Fact]
    public void O_mangá_atende_no_slash_mg()
    {
        var slash = typeof(MangadexCommand)
            .GetMethods()
            .SelectMany(método => método.GetCustomAttributes<SlashCommandAttribute>());

        Assert.Equal("mg", Assert.Single(slash).Name);
    }

    [Fact]
    public void No_prefixo_atende_mg_e_manga()
    {
        var atalhos = CommandAudit.AliasesDeTexto(typeof(MangadexTextCommand)).ToList();

        Assert.Contains("mg", atalhos);
        Assert.Contains("manga", atalhos);
    }

    // O /ma tinha subcomando porque eram dois; sobrou o anime, e o nome vai direto.
    [Fact]
    public void O_ma_não_tem_mais_subcomando()
    {
        var subs = typeof(MyAnimelistCommand)
            .GetMethods()
            .SelectMany(método => método.GetCustomAttributes<SubSlashCommandAttribute>());

        Assert.Empty(subs);
    }

    // "!ma anime Naruto" era obrigatório e continua valendo; mangá vira recado, não busca.
    [Theory]
    [InlineData("naruto", "naruto", false)]
    [InlineData("one piece", "one piece", false)]
    [InlineData("anime naruto", "naruto", false)]
    [InlineData("Animes Naruto", "Naruto", false)]
    [InlineData("manga berserk", "berserk", true)]
    [InlineData("mangá berserk", "berserk", true)]
    [InlineData("MANGÁS Berserk", "Berserk", true)]
    [InlineData("manga", "manga", false)]
    public void Lê_o_pedido_do_ma(string texto, string busca, bool éMangá)
    {
        Assert.Equal((busca, éMangá), MyAnimelist.LerPedido(texto));
    }

    // O recado tem que ser o comando pronto pra copiar, com o prefixo do servidor.
    [Fact]
    public void Quem_pediu_mangá_no_ma_recebe_o_comando_novo()
    {
        Assert.Contains("!mg berserk", Denia.MalMangáÉOutroComando("!", "berserk"));
    }
}

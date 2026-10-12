using AlephBot.Threnodian.Dados;
using AlephBot.Threnodian.MyAnimeList;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Dados;

/// <summary>
/// A guarda das fichas do /ma. O que importa aqui é que ela seja conveniência e não
/// dependência: sem Postgres o comando tem que funcionar como funcionava antes dela existir.
/// </summary>
public class ObrasEmCacheTests
{
    // fábrica e migração nulas: é o bot subindo sem DATABASE_URL
    private static ObrasEmCache SemBanco() => new(null, null, NullLogger<ObrasEmCache>.Instance);

    [Fact]
    public void Sem_banco_a_guarda_se_declara_indisponível()
    {
        Assert.False(SemBanco().Disponível);
    }

    [Fact]
    public async Task Sem_banco_não_acha_ficha_nenhuma()
    {
        var achada = await SemBanco().AcharAsync(
            TipoDeObra.Anime, "naruto", TimeSpan.FromDays(7), TestContext.Current.CancellationToken);

        Assert.Null(achada);
    }

    /// <summary>Guardar sem banco não pode explodir: quem chamou está no meio de responder alguém.</summary>
    [Fact]
    public async Task Sem_banco_guardar_não_levanta_erro()
    {
        var obra = new Obra { Tipo = TipoDeObra.Anime, Id = 20, Título = "NARUTO" };

        await SemBanco().GuardarAsync("naruto", obra, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// O termo é a chave da busca guardada: "NARUTO ", "naruto" e "Naruto" são a mesma
    /// pergunta, e acento não pode separar o que é igual.
    /// </summary>
    [Theory]
    [InlineData("NARUTO", "naruto")]
    [InlineData("  Naruto  ", "naruto")]
    [InlineData("Shingeki no Kyojin", "shingeki no kyojin")]
    [InlineData("Mangá   do    Berserk", "manga do berserk")]
    [InlineData("Fullmetal Alchemist: Brotherhood", "fullmetal alchemist: brotherhood")]
    public void Normaliza_o_que_a_pessoa_digitou(string digitado, string esperado)
    {
        Assert.Equal(esperado, ObrasEmCache.Normaliza(digitado));
    }

    /// <summary>
    /// A linha de procura junta todos os nomes da obra, pra ficha ser achada pelo título em
    /// inglês, pelo japonês ou por um sinônimo quando a fonte estiver fora.
    /// </summary>
    [Fact]
    public void A_linha_de_procura_junta_todos_os_nomes()
    {
        var obra = new Obra
        {
            Tipo = TipoDeObra.Anime,
            Id = 1,
            Título = "Shingeki no Kyojin",
            TítuloEmInglês = "Attack on Titan",
            TítuloEmJaponês = "進撃の巨人",
            Sinônimos = ["AoT"],
        };

        var procura = ObrasEmCache.LinhaDeProcura(obra);

        Assert.Contains("shingeki no kyojin", procura);
        Assert.Contains("attack on titan", procura);
        Assert.Contains("aot", procura);
        Assert.Contains("進撃の巨人", procura);
    }
}

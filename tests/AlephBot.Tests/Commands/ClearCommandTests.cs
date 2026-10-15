using AlephBot.Core.Commands.Admin;
using AlephBot.Core.Personality;

using NetCord;

namespace AlephBot.Tests.Commands;

// A faxina sem Discord: o que entra no lote de apagar, e o que o embed conta de volta.
public class ClearCommandTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    // O id do Discord carrega a hora dentro dele, e é dela que sai a idade da mensagem.
    private static (ulong Id, bool Fixada) Mensagem(TimeSpan idade, bool fixada = false) =>
        (Snowflake.Create(Agora - idade), fixada);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(101)]
    public void Fora_da_faixa_eu_nem_tento(int quantidade)
    {
        Assert.Equal(Denia.ClearQuantidadeInválida(1, 100), Limpeza.Recusa(quantidade));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Dentro_da_faixa_segue(int quantidade)
    {
        Assert.Null(Limpeza.Recusa(quantidade));
    }

    [Fact]
    public void Mensagem_fixada_continua_onde_está()
    {
        var (ids, saldo) = Limpeza.Separa(
            [Mensagem(TimeSpan.FromMinutes(1)), Mensagem(TimeSpan.FromMinutes(2), fixada: true)], Agora);

        Assert.Single(ids);
        Assert.Equal(1, saldo.Apagadas);
        Assert.Equal(1, saldo.Fixadas);
        Assert.Equal(0, saldo.Velhas);
    }

    // O lote do Discord recusa o que passou de 14 dias; ela fica, e o embed explica por quê.
    [Fact]
    public void Mensagem_de_mais_de_catorze_dias_não_entra_no_lote()
    {
        var (ids, saldo) = Limpeza.Separa(
            [Mensagem(TimeSpan.FromDays(15)), Mensagem(TimeSpan.FromDays(13))], Agora);

        Assert.Single(ids);
        Assert.Equal(1, saldo.Velhas);
        Assert.Equal(0, saldo.Fixadas);
    }

    [Fact]
    public void Canal_sem_nada_pra_apagar_não_monta_lote()
    {
        var (ids, saldo) = Limpeza.Separa([], Agora);

        Assert.Empty(ids);
        Assert.Equal(0, saldo.Apagadas);
    }

    [Fact]
    public void O_embed_diz_quantas_sairam()
    {
        var embed = Limpeza.Embed(new Limpeza.Saldo(Apagadas: 7, Fixadas: 0, Velhas: 0), "ariii");

        Assert.Equal(Denia.ClearTítulo, embed.Title);
        Assert.Contains("7", embed.Description!);
        Assert.Contains("ariii", embed.Footer!.Text!);
    }

    // Campo vazio é sujeira: sem fixada nem velha, o embed é só a conta do que saiu.
    [Fact]
    public void Sem_sobra_o_embed_não_ganha_campo()
    {
        var embed = Limpeza.Embed(new Limpeza.Saldo(Apagadas: 3, Fixadas: 0, Velhas: 0), "ariii");

        Assert.Empty(embed.Fields!);
    }

    [Fact]
    public void O_que_ficou_pra_trás_vira_campo()
    {
        var embed = Limpeza.Embed(new Limpeza.Saldo(Apagadas: 3, Fixadas: 1, Velhas: 2), "ariii");

        var nomes = embed.Fields!.Select(campo => campo.Name).ToList();

        Assert.Equal([Denia.ClearCampoFixadas, Denia.ClearCampoVelhas], nomes);
    }
}

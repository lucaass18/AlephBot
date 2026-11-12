using AlephBot.Config;
using AlephBot.Core.Commands.Admin;
using AlephBot.Core.Commands.Music;
using AlephBot.Threnodian.Api;

using NetCord;

namespace AlephBot.Tests.Fronteiras;

// Os valores que ficam exatamente na borda de cada regra: é onde um >= trocado por > passa calado.
public class ValoresDeFronteiraTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    // A faixa de porta válida vai de 1 a 65535; os testes de antes só olhavam o lado de fora dela.
    [Theory]
    [InlineData("1", 1)]
    [InlineData("65535", 65535)]
    public void API_PORT_aceita_as_duas_pontas_da_faixa(string valor, int porta)
    {
        var config = AlephConfig.De(nome => nome switch
        {
            "TOKEN" => "token-de-teste",
            "API_KEY" => "0123456789abcdef",
            "API_PORT" => valor,
            _ => null,
        });

        Assert.Equal(porta, config.Api!.Port);
    }

    // O Discord recusa em lote o que tem 14 dias ou mais: no limite exato a mensagem fica.
    [Fact]
    public void Mensagem_com_exatamente_catorze_dias_fica_e_com_um_segundo_a_menos_sai()
    {
        var noLimite = Snowflake.Create(Agora - Limpeza.Idade);
        var quaseLá = Snowflake.Create(Agora - Limpeza.Idade + TimeSpan.FromSeconds(1));

        var (ids, saldo) = Limpeza.Separa([(noLimite, false), (quaseLá, false)], Agora);

        Assert.Equal([quaseLá], ids);
        Assert.Equal(1, saldo.Velhas);
    }

    // O corte contra estouro é em 9.999.999: ele ainda passa, e o número seguinte já não.
    [Fact]
    public void A_duração_do_mute_lê_até_9999999_e_recusa_o_número_seguinte()
    {
        Assert.Equal(TimeSpan.FromSeconds(9_999_999), MuteCommand.ParseDuração("9999999s"));
        Assert.Null(MuteCommand.ParseDuração("10000000s"));
    }

    [Fact]
    public void A_posição_do_seek_lê_até_9999999_e_recusa_o_número_seguinte()
    {
        Assert.Equal(TimeSpan.FromSeconds(9_999_999), Music.ParsePosição("9999999"));
        Assert.Null(Music.ParsePosição("10000000"));
    }

    // Pedido sem IP ainda precisa de um nome pra dividir a cota; era o caminho que ninguém exercitava.
    [Fact]
    public void Pedido_sem_IP_divide_a_cota_com_o_nome_sem_ip()
    {
        Assert.Equal("sem ip", LimiteDePedidos.ClienteDe(null));
    }
}

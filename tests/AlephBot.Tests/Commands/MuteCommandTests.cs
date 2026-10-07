using AlephBot.Core.Commands.Admin;
using AlephBot.Core.Personality;

namespace AlephBot.Tests.Commands;

/// <summary>A duração do /mute: o que a pessoa digita vira tempo, e o tempo vira português.</summary>
public class MuteCommandTests
{
    [Theory]
    [InlineData("30s", 30)]
    [InlineData("10m", 600)]
    [InlineData("2h", 7_200)]
    [InlineData("7d", 604_800)]
    [InlineData("1h30m", 5_400)]
    [InlineData("2H", 7_200)]
    [InlineData(" 1 h ", 3_600)]
    public void Lê_unidade_e_mistura_de_unidades(string texto, int segundos)
    {
        Assert.Equal(TimeSpan.FromSeconds(segundos), MuteCommand.ParseDuração(texto));
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData("1h30", 90)]
    public void Número_sem_unidade_fecha_em_minutos(string texto, int minutos)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutos), MuteCommand.ParseDuração(texto));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("mh")]
    [InlineData("h30")]
    [InlineData("10x")]
    [InlineData("-5m")]
    [InlineData("0")]
    [InlineData("0m")]
    [InlineData("99999999m")]
    public void O_que_não_vira_tempo_positivo_é_null(string texto)
    {
        Assert.Null(MuteCommand.ParseDuração(texto));
    }

    [Fact]
    public void Mais_de_um_ano_para_no_teto()
    {
        Assert.Equal(TimeSpan.FromDays(365), MuteCommand.ParseDuração("400d"));
    }

    [Fact]
    public void Validar_respeita_o_limite_do_Discord()
    {
        Assert.Equal(TimeSpan.FromDays(MuteCommand.MaxDias), MuteCommand.ValidateDuração($"{MuteCommand.MaxDias}d"));
        Assert.Null(MuteCommand.ValidateDuração($"{MuteCommand.MaxDias + 1}d"));
        Assert.Null(MuteCommand.ValidateDuração("abc"));
    }

    [Fact]
    public void O_erro_diz_se_não_entendeu_ou_se_passou_do_limite()
    {
        Assert.Equal(Denia.DuraçãoInválida(MuteCommand.Exemplo), MuteCommand.ErroDaDuração("abc"));
        Assert.Equal(Denia.DuraçãoLongaDemais(MuteCommand.MaxDias), MuteCommand.ErroDaDuração("60d"));
    }

    [Theory]
    [InlineData(0, "menos de um segundo")]
    [InlineData(1, "1 segundo")]
    [InlineData(60, "1 minuto")]
    [InlineData(90, "1 minuto e 30 segundos")]
    [InlineData(3_600, "1 hora")]
    [InlineData(3_661, "1 hora e 1 minuto")]
    [InlineData(3_601, "1 hora e 1 segundo")]
    [InlineData(2 * 86_400 + 3 * 3_600 + 4 * 60, "2 dias e 3 horas")]
    public void Humaniza_com_as_duas_maiores_unidades(int segundos, string esperado)
    {
        Assert.Equal(esperado, MuteCommand.Humanize(TimeSpan.FromSeconds(segundos)));
    }

    [Fact]
    public void Descreve_o_silêncio_com_o_relógio_do_Discord()
    {
        var até = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        Assert.Equal("10 minutos • <t:1700000000:R>", MuteCommand.DescreveSilêncio(TimeSpan.FromMinutes(10), até));
    }
}

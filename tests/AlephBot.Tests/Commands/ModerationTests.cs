using AlephBot.Core.Commands.Admin;
using AlephBot.Core.Commands.Interaction;
using AlephBot.Core.Personality;

using NetCord;

namespace AlephBot.Tests.Commands;

public class ModerationTests
{
    [Theory]
    [InlineData(AçãoModeração.Kick, 0xE67E22)]
    [InlineData(AçãoModeração.Mute, 0x9B59B6)]
    [InlineData(AçãoModeração.Ban, 0xE74C3C)]
    [InlineData(AçãoModeração.Unmute, 0x2ECC71)]
    [InlineData(AçãoModeração.Unban, 0x2ECC71)]
    public void Cada_ação_tem_sua_cor(AçãoModeração ação, int cor)
    {
        Assert.Equal(cor, Moderation.ColorFor(ação));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_motivo_vira_o_texto_padrão(string? motivo)
    {
        Assert.Equal(Denia.SemMotivo(), Moderation.NormalizeReason(motivo));
    }

    [Fact]
    public void Motivo_perde_os_espaços_das_pontas()
    {
        Assert.Equal("spam no geral", Moderation.NormalizeReason("  spam no geral  "));
    }
}

/// <summary>A cor do /ping acompanha a latência: verde, amarelo, vermelho.</summary>
public class PingCommandTests
{
    [Theory]
    [InlineData(0, 0x2ECC71)]
    [InlineData(149, 0x2ECC71)]
    [InlineData(150, 0xF1C40F)]
    [InlineData(299, 0xF1C40F)]
    [InlineData(300, 0xE74C3C)]
    [InlineData(2_000, 0xE74C3C)]
    public void Cor_muda_em_150_e_em_300_ms(int ms, int cor)
    {
        var embed = PingCommand.BuildEmbed(TimeSpan.FromMilliseconds(ms));

        Assert.Equal(new Color(cor), embed.Color);
    }

    [Fact]
    public void Mostra_a_latência_em_ms()
    {
        var embed = PingCommand.BuildEmbed(TimeSpan.FromMilliseconds(42));

        Assert.Contains("**42 ms**", embed.Description);
    }
}

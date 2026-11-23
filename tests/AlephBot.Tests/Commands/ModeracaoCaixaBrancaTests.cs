using AlephBot.Core.Commands.Admin;
using AlephBot.Core.Personality;

using static AlephBot.Tests.Commands.DiscordFalso;

namespace AlephBot.Tests.Commands;

// Caixa branca: um teste para cada caminho das funções de moderação que só leem dados.
// O servidor e os membros são montados na memória (DiscordFalso), sem Discord.
public class ValidaçãoDaModeraçãoTests
{
    private const ulong Alvo = 200;
    private const ulong Moderador = 300;

    // cargos do servidor de teste: 10 é o mais baixo, 30 o mais alto
    private const ulong Baixo = 10, Médio = 20, Alto = 30;
    private static readonly NetCord.Gateway.Guild Servidor = DiscordFalso.Servidor((Baixo, 1), (Médio, 2), (Alto, 3));

    private static string? Valida(NetCord.GuildUser alvo, NetCord.GuildUser moderador) =>
        Moderation.Validate(Servidor, alvo, moderador, BotId, AçãoModeração.Ban);

    // caminho 1: if (alvo.Id == moderador.Id)
    [Fact]
    public void Punir_a_si_mesmo_é_recusado()
    {
        var eu = Membro(Moderador, Alto);

        Assert.Equal(Denia.RecusaPróprio(AçãoModeração.Ban), Valida(eu, eu));
    }

    // caminho 2: if (alvo.Id == botId)
    [Fact]
    public void Punir_o_bot_é_recusado()
    {
        Assert.Equal(Denia.RecusaBot(AçãoModeração.Ban), Valida(Membro(BotId), Membro(Moderador, Alto)));
    }

    // caminho 3: if (alvo.Id == guild.OwnerId)
    [Fact]
    public void Punir_o_dono_é_recusado_mesmo_por_cargo_mais_alto()
    {
        Assert.Equal(Denia.RecusaDono(), Valida(Membro(DonoId), Membro(Moderador, Alto)));
    }

    // caminho 4: moderador.Id == guild.OwnerId → a hierarquia nem é olhada
    [Fact]
    public void Dono_pune_quem_tem_cargo_mais_alto_que_o_dele()
    {
        Assert.Null(Valida(Membro(Alvo, Alto), Membro(DonoId)));
    }

    // caminho 5: TopRole(alvo) é null → só tem o @everyone, qualquer moderador passa
    [Fact]
    public void Alvo_sem_cargo_pode_ser_punido_por_quem_também_não_tem()
    {
        Assert.Null(Valida(Membro(Alvo), Membro(Moderador)));
    }

    // caminho 6: TopRole(moderador) is not { } → recusa
    [Fact]
    public void Moderador_sem_cargo_não_pune_quem_tem()
    {
        var alvo = Membro(Alvo, Baixo);

        Assert.Equal(Denia.HierarquiaInsuficiente(alvo.ToString()), Valida(alvo, Membro(Moderador)));
    }

    // caminho 7: cargoModerador.Position <= cargoAlvo.Position, nas duas pontas do <=
    [Theory]
    [InlineData(Baixo, Alto)]   // abaixo
    [InlineData(Médio, Médio)]  // igual: o limite da comparação
    public void Moderador_com_cargo_igual_ou_abaixo_é_recusado(ulong cargoModerador, ulong cargoAlvo)
    {
        var alvo = Membro(Alvo, cargoAlvo);

        Assert.Equal(Denia.HierarquiaInsuficiente(alvo.ToString()), Valida(alvo, Membro(Moderador, cargoModerador)));
    }

    // caminho 8: passou por todos os if → null, a punição pode seguir
    [Theory]
    [InlineData(Médio, Baixo)]  // uma posição acima: o outro lado do limite
    [InlineData(Alto, Baixo)]
    public void Moderador_com_cargo_acima_pode_punir(ulong cargoModerador, ulong cargoAlvo)
    {
        Assert.Null(Valida(Membro(Alvo, cargoAlvo), Membro(Moderador, cargoModerador)));
    }

    // a mesma ordem de checagem vale para as outras punições: o texto muda com a ação
    [Theory]
    [InlineData(AçãoModeração.Kick)]
    [InlineData(AçãoModeração.Mute)]
    public void Recusa_de_si_mesmo_fala_da_ação_pedida(AçãoModeração ação)
    {
        var eu = Membro(Moderador, Alto);

        Assert.Equal(Denia.RecusaPróprio(ação), Moderation.Validate(Servidor, eu, eu, BotId, ação));
    }
}

public class CargoMaisAltoTests
{
    private static readonly NetCord.Gateway.Guild Servidor = DiscordFalso.Servidor((10, 1), (20, 5), (30, 3));

    // o foreach não roda: nenhum cargo além do @everyone
    [Fact]
    public void Sem_cargo_devolve_null()
    {
        Assert.Null(Moderation.TopRole(Servidor, Membro(200)));
    }

    // o if troca o "top" só quando acha posição maior, em qualquer ordem
    [Theory]
    [InlineData(new ulong[] { 10, 20, 30 })]
    [InlineData(new ulong[] { 30, 20, 10 })]
    [InlineData(new ulong[] { 20 })]
    public void Devolve_o_de_maior_posição(ulong[] cargos)
    {
        Assert.Equal(20UL, Moderation.TopRole(Servidor, Membro(200, cargos))!.Id);
    }

    // TryGetValue falha: cargo que o servidor não conhece (apagado) é ignorado
    [Fact]
    public void Cargo_que_o_servidor_não_tem_é_ignorado()
    {
        Assert.Equal(10UL, Moderation.TopRole(Servidor, Membro(200, 999, 10))!.Id);
        Assert.Null(Moderation.TopRole(Servidor, Membro(200, 999)));
    }
}

public class RegistroDeAuditoriaTests
{
    [Fact]
    public void Motivo_leva_o_nome_de_quem_mandou()
    {
        var pedido = Moderation.AuditLog(Membro(300), "spam no geral");

        Assert.Equal("usuario300: spam no geral", pedido.AuditLogReason);
    }
}

public class IdDoUnbanTests
{
    // ParseId: limpa espaços e os sinais de menção, depois tenta ler o número
    [Theory]
    [InlineData("123456789012345678", 123456789012345678UL)]
    [InlineData("  123  ", 123UL)]
    [InlineData("<@123>", 123UL)]
    [InlineData("<@!123>", 123UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]  // o maior que cabe
    public void Lê_id_cru_e_menção(string texto, ulong id)
    {
        Assert.Equal(id, UnBanCommand.ParseId(texto));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("12 34")]
    [InlineData("18446744073709551616")]  // um a mais que o maior: não cabe
    public void Texto_que_não_é_id_devolve_null(string texto)
    {
        Assert.Null(UnBanCommand.ParseId(texto));
    }

    // Validate: quatro caminhos, um para cada return
    [Fact]
    public void Id_ilegível_é_o_primeiro_erro()
    {
        Assert.Equal((null, Denia.IdInválido()), UnBanCommand.Validate("abc", 300, BotId));
    }

    [Fact]
    public void Desbanir_a_si_mesmo_é_recusado()
    {
        Assert.Equal((null, Denia.RecusaPróprio(AçãoModeração.Unban)), UnBanCommand.Validate("300", 300, BotId));
    }

    [Fact]
    public void Desbanir_o_bot_é_recusado()
    {
        Assert.Equal((null, Denia.RecusaBot(AçãoModeração.Unban)), UnBanCommand.Validate($"<@{BotId}>", 300, BotId));
    }

    [Fact]
    public void Id_de_outra_pessoa_segue()
    {
        Assert.Equal((200UL, (string?)null), UnBanCommand.Validate("200", 300, BotId));
    }
}

public class TempoDeSilêncioTests
{
    // if (alvo.TimeOutUntil is not { }) → nunca foi silenciado
    [Fact]
    public void Sem_silêncio_devolve_null()
    {
        Assert.Null(UnMuteCommand.RestoAtéSoltar(Membro(200)));
    }

    // restante <= 0: o Discord deixa a data antiga lá depois que o silêncio acaba
    [Fact]
    public void Silêncio_que_já_acabou_devolve_null()
    {
        Assert.Null(UnMuteCommand.RestoAtéSoltar(Membro(200, DateTimeOffset.UtcNow.AddMinutes(-1))));
    }

    // restante > 0: devolve quanto falta
    [Fact]
    public void Silêncio_em_andamento_devolve_quanto_falta()
    {
        var resto = UnMuteCommand.RestoAtéSoltar(Membro(200, DateTimeOffset.UtcNow.AddHours(1)));

        Assert.NotNull(resto);
        Assert.InRange(resto.Value, TimeSpan.FromMinutes(59), TimeSpan.FromHours(1));
    }
}

public class LimpezaDoBanTests
{
    // um caso para cada braço do switch
    [Theory]
    [InlineData(0, "Nenhuma")]
    [InlineData(1, "Último dia")]
    [InlineData(2, "Últimos 2 dias")]
    [InlineData(7, "Últimos 7 dias")]
    public void Descreve_quantos_dias_de_mensagens_somem(int dias, string texto)
    {
        Assert.Equal(texto, BanCommand.DescreveLimpeza(dias));
    }
}

using AlephBot.Config;
using AlephBot.Core.Commands.Admin;
using AlephBot.Threnodian.Dados;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Dados;

/// <summary>
/// A regra que o banco não pode quebrar: sem ele, o bot segue inteiro. Tocar música e
/// responder comando não passam por Postgres — ele guarda o que cada servidor escolheu, e
/// servidor sem escolha (ou com o banco fora) fica com o padrão do .env, que é exatamente
/// como o bot funcionava antes de existir banco aqui.
/// </summary>
public class ConfigDosServidoresTests
{
    private const ulong Servidor = 1071673946337972307;

    private static ConfigDosServidores SemBanco(string prefixo = "!", string espera = "30")
    {
        var config = AlephConfig.De(nome => nome switch
        {
            "TOKEN" => $"{Convert.ToBase64String("123456789012345678"u8)}.teste.teste",
            "PREFIX" => prefixo,
            "MUSIC_IDLE_MINUTES" => espera,
            _ => null,
        });

        // fábrica e migração nulas: é o bot subindo sem DATABASE_URL
        return new ConfigDosServidores(null, null, config, NullLogger<ConfigDosServidores>.Instance);
    }

    [Fact]
    public async Task Sem_banco_o_prefixo_é_o_do_env()
    {
        var configs = SemBanco(prefixo: "?");

        Assert.Equal("?", await configs.PrefixoAsync(Servidor));
    }

    [Fact]
    public async Task Sem_banco_não_há_canal_de_log()
    {
        Assert.Null(await SemBanco().CanalDeLogAsync(Servidor));
    }

    [Fact]
    public async Task Sem_banco_a_espera_da_música_é_a_do_env()
    {
        var configs = SemBanco(espera: "12");

        Assert.Equal(TimeSpan.FromMinutes(12), await configs.EsperaDaMúsicaAsync(Servidor));
    }

    [Fact]
    public async Task Sem_banco_salvar_devolve_false_em_vez_de_fingir()
    {
        // quem chamou precisa saber pra avisar a pessoa: dizer "guardei" sem ter guardado
        // é pior do que não guardar
        var guardou = await SemBanco().SalvaAsync(Servidor, linha => linha.Prefixo = "?");

        Assert.False(guardou);
    }

    [Fact]
    public async Task Sem_banco_a_linha_do_servidor_é_nula_e_ninguém_estoura()
    {
        Assert.Null(await SemBanco().DoServidorAsync(Servidor));
    }
}

/// <summary>
/// O que o /config aceita. Prefixo ruim trava o servidor inteiro: se o bot passar a
/// responder a um prefixo impossível, nem o comando de texto pra desfazer funciona.
/// </summary>
public class ConfigCommandTests
{
    [Theory]
    [InlineData("!")]
    [InlineData("?")]
    [InlineData("aleph")]
    [InlineData("!!")]
    [InlineData("1234567890123456")]
    public void Prefixo_curto_e_sem_espaço_passa(string prefixo)
    {
        Assert.True(ConfigCommand.ÉPrefixoAceitável(prefixo));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aleph bot")]
    [InlineData("12345678901234567")]
    public void Prefixo_vazio_com_espaço_ou_longo_demais_não_passa(string prefixo)
    {
        Assert.False(ConfigCommand.ÉPrefixoAceitável(prefixo));
    }

    [Theory]
    [InlineData("padrão")]
    [InlineData("PADRÃO")]
    [InlineData("  padrão  ")]
    public void A_palavra_padrão_volta_pro_prefixo_do_bot(string valor)
    {
        Assert.True(ConfigCommand.ÉPadrão(valor));
        Assert.True(ConfigCommand.ÉPrefixoAceitável(valor));
    }

    [Fact]
    public void Prefixo_qualquer_não_é_a_palavra_padrão()
    {
        Assert.False(ConfigCommand.ÉPadrão("!"));
    }
}

/// <summary>
/// Como o bot decide se uma mensagem é comando. Quem fazia essa conta era o NetCord, pelo
/// options.Prefix; desde que o prefixo passou a mudar por servidor, a conta é nossa — e o
/// que o NetCord fazia de graça tem que continuar valendo.
/// </summary>
public class TamanhoDoPrefixoTests
{
    [Theory]
    [InlineData("!ping", "!", 1)]
    [InlineData("?play algo", "?", 1)]
    [InlineData("alephping", "aleph", 5)]
    [InlineData("!!ping", "!!", 2)]
    public void O_prefixo_certo_diz_onde_o_comando_começa(string conteúdo, string prefixo, int tamanho)
    {
        Assert.Equal(tamanho, AlephBot.Threnodian.Inicialização.AlephBot.TamanhoDoPrefixo(conteúdo, prefixo));
    }

    [Theory]
    [InlineData("ping", "!")]
    [InlineData("oi, tudo bem?", "!")]
    [InlineData("", "!")]
    [InlineData("?ping", "!")]
    public void Mensagem_que_não_começa_com_o_prefixo_não_é_comando(string conteúdo, string prefixo)
    {
        Assert.True(AlephBot.Threnodian.Inicialização.AlephBot.TamanhoDoPrefixo(conteúdo, prefixo) < 0);
    }

    [Theory]
    [InlineData("ALEPHping")]
    [InlineData("Alephping")]
    public void A_caixa_do_prefixo_não_importa(string conteúdo)
    {
        Assert.Equal(5, AlephBot.Threnodian.Inicialização.AlephBot.TamanhoDoPrefixo(conteúdo, "aleph"));
    }

    [Fact]
    public void O_negativo_é_o_que_o_NetCord_entende_como_não_é_pra_mim()
    {
        // o NetCord testa "< 0" antes de cortar o prefixo fora do texto; um 0 aqui faria
        // ele tratar a mensagem inteira como comando
        Assert.True(AlephBot.Threnodian.Inicialização.AlephBot.NãoÉComando < 0);
    }
}

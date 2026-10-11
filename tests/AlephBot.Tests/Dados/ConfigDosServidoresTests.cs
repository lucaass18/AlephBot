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

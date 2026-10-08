using AlephBot.Config;

using Microsoft.Extensions.Logging;

namespace AlephBot.Tests.Config;

/// <summary>
/// A config lida de um dicionário, não do ambiente: o teste não depende do .env de quem roda
/// e não pisa nas variáveis dos outros testes.
/// </summary>
public class AlephConfigTests
{
    private const string Chave16 = "0123456789abcdef";

    private static AlephConfig Carregar(params (string Nome, string? Valor)[] variáveis)
    {
        var mapa = variáveis.ToDictionary(v => v.Nome, v => v.Valor);
        return AlephConfig.De(nome => mapa.GetValueOrDefault(nome));
    }

    private static AlephConfig ComToken(params (string Nome, string? Valor)[] extras) =>
        Carregar([("TOKEN", "token-de-teste"), .. extras]);

    [Fact]
    public void Sem_TOKEN_não_sobe()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => Carregar());
        Assert.Contains("TOKEN", erro.Message);
    }

    [Fact]
    public void TOKEN_em_branco_conta_como_ausente()
    {
        Assert.Throws<InvalidOperationException>(() => Carregar(("TOKEN", "   ")));
    }

    [Fact]
    public void Só_com_o_TOKEN_o_resto_cai_no_padrão()
    {
        var config = ComToken();

        Assert.Equal("token-de-teste", config.Token);
        Assert.Equal("!", config.Prefix);
        Assert.Null(config.DevGuildId);
        Assert.False(config.IsDevelopment);
        Assert.Equal(LogLevel.Information, config.LogLevel);
        Assert.Equal(new Uri("http://localhost:2333/"), config.LavalinkUri);
        Assert.Equal("youshallnotpass", config.LavalinkPassword);
        Assert.Equal(TimeSpan.FromMinutes(2), config.MusicIdleTimeout);
        Assert.False(config.YoutubeLogin);
        Assert.Null(config.YoutubeRefreshToken);
        Assert.Null(config.MalClientId);
        Assert.Null(config.Api);
    }

    [Fact]
    public void Lê_o_que_veio_preenchido()
    {
        var config = ComToken(
            ("PREFIX", "?"),
            ("LAVALINK_URI", "http://lavalink:2333/"),
            ("LAVALINK_PASSWORD", "senha"),
            ("YOUTUBE_REFRESH_TOKEN", "1//refresh"),
            ("MAL_CLIENT_ID", "client-id"));

        Assert.Equal("?", config.Prefix);
        Assert.Equal(new Uri("http://lavalink:2333/"), config.LavalinkUri);
        Assert.Equal("senha", config.LavalinkPassword);
        Assert.Equal("1//refresh", config.YoutubeRefreshToken);
        Assert.Equal("client-id", config.MalClientId);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData(" yes ")]
    [InlineData("sim")]
    [InlineData("on")]
    public void YOUTUBE_LOGIN_aceita_o_jeito_de_quem_escreve(string valor)
    {
        Assert.True(ComToken(("YOUTUBE_LOGIN", valor)).YoutubeLogin);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("não")]
    [InlineData("")]
    public void YOUTUBE_LOGIN_desligado(string valor)
    {
        Assert.False(ComToken(("YOUTUBE_LOGIN", valor)).YoutubeLogin);
    }

    [Fact]
    public void DEV_GUILD_ID_liga_o_modo_dev()
    {
        var config = ComToken(("DEV_GUILD_ID", "1071673946337972307"));

        Assert.Equal(1071673946337972307UL, config.DevGuildId);
        Assert.True(config.IsDevelopment);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    public void Em_produção_DEV_GUILD_ID_não_liga_o_modo_dev(string ambiente)
    {
        var config = ComToken(("DEV_GUILD_ID", "1071673946337972307"), ("DOTNET_ENVIRONMENT", ambiente));

        Assert.True(config.IsProduction);
        Assert.False(config.IsDevelopment);
        Assert.Equal(1071673946337972307UL, config.DevGuildId);
    }

    [Fact]
    public void Fora_de_produção_o_ambiente_não_atrapalha_o_dev()
    {
        var config = ComToken(("DEV_GUILD_ID", "1"), ("DOTNET_ENVIRONMENT", "Development"));

        Assert.False(config.IsProduction);
        Assert.True(config.IsDevelopment);
    }

    [Fact]
    public void DEV_GUILD_ID_torto_fica_de_fora()
    {
        Assert.Null(ComToken(("DEV_GUILD_ID", "abc")).DevGuildId);
    }

    [Theory]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("Warning", LogLevel.Warning)]
    [InlineData("qualquer coisa", LogLevel.Information)]
    public void LOG_LEVEL_não_liga_pra_maiúscula(string valor, LogLevel esperado)
    {
        Assert.Equal(esperado, ComToken(("LOG_LEVEL", valor)).LogLevel);
    }

    [Fact]
    public void LAVALINK_URI_torto_é_erro_e_não_cai_no_padrão()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ComToken(("LAVALINK_URI", "não é endereço")));
        Assert.Contains("LAVALINK_URI", erro.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_YOUTUBE_AUDIO_URI_o_YouTube_fica_com_o_plugin(string? valor)
    {
        Assert.Null(ComToken(("YOUTUBE_AUDIO_URI", valor)).YoutubeAudioUri);
    }

    [Fact]
    public void YOUTUBE_AUDIO_URI_é_o_endereço_do_yt_audio()
    {
        var config = ComToken(("YOUTUBE_AUDIO_URI", "http://yt-audio:8080/"));
        Assert.Equal(new Uri("http://yt-audio:8080/"), config.YoutubeAudioUri);
    }

    [Fact]
    public void YOUTUBE_AUDIO_URI_torto_é_erro_e_não_desliga_calado()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ComToken(("YOUTUBE_AUDIO_URI", "não é endereço")));
        Assert.Contains("YOUTUBE_AUDIO_URI", erro.Message);
    }

    [Theory]
    [InlineData("5", 300)]
    [InlineData("0.5", 30)]
    [InlineData("0", 120)]
    [InlineData("-1", 120)]
    [InlineData("abc", 120)]
    public void MUSIC_IDLE_MINUTES_aceita_fração_e_ignora_o_que_não_é_positivo(string valor, int segundos)
    {
        Assert.Equal(TimeSpan.FromSeconds(segundos), ComToken(("MUSIC_IDLE_MINUTES", valor)).MusicIdleTimeout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_API_KEY_a_API_não_existe(string? valor)
    {
        Assert.Null(ComToken(("API_KEY", valor)).Api);
    }

    [Fact]
    public void API_KEY_curta_é_erro()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ComToken(("API_KEY", Chave16[..15])));
        Assert.Contains("API_KEY", erro.Message);
    }

    [Fact]
    public void API_KEY_de_16_caracteres_liga_a_API_na_porta_padrão()
    {
        var api = ComToken(("API_KEY", Chave16)).Api;

        Assert.NotNull(api);
        Assert.Equal(Chave16, api.Key);
        Assert.Equal(8080, api.Port);
        Assert.Null(api.ScalarAgentKey);
    }

    [Fact]
    public void API_PORT_SCALAR_AGENT_KEY_e_API_DOCS_PASSWORD_vão_pra_config_da_API()
    {
        var api = ComToken(
            ("API_KEY", Chave16),
            ("API_PORT", "9000"),
            ("SCALAR_AGENT_KEY", "agent"),
            ("API_DOCS_PASSWORD", "senha")).Api;

        Assert.NotNull(api);
        Assert.Equal(9000, api.Port);
        Assert.Equal("agent", api.ScalarAgentKey);
        Assert.Equal("senha", api.DocsPassword);
    }

    [Fact]
    public void Sem_API_DOCS_PASSWORD_os_docs_ficam_abertos()
    {
        Assert.Null(ComToken(("API_KEY", Chave16)).Api!.DocsPassword);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void API_PORT_torta_é_erro(string valor)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ComToken(("API_KEY", Chave16), ("API_PORT", valor)));
        Assert.Contains("API_PORT", erro.Message);
    }

    [Fact]
    public void ApiConfig_não_imprime_a_chave()
    {
        // é classe e não record justamente pra um log descuidado da config não vazar a chave
        var api = new ApiConfig(Chave16, 8080);
        Assert.DoesNotContain(Chave16, api.ToString());
    }
}

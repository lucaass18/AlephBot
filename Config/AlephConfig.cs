using System.Globalization;

using Microsoft.Extensions.Logging;

namespace AlephBot.Config;

public sealed class AlephConfig
{
    private AlephConfig(
        string token,
        string prefix,
        ulong? devGuildId,
        LogLevel logLevel,
        Uri lavalinkUri,
        string lavalinkPassword,
        TimeSpan musicIdleTimeout,
        bool youtubeLogin,
        string? youtubeRefreshToken,
        string? malClientId,
        ApiConfig? api,
        string? ambiente)
    {
        Token = token;
        Prefix = prefix;
        DevGuildId = devGuildId;
        LogLevel = logLevel;
        LavalinkUri = lavalinkUri;
        LavalinkPassword = lavalinkPassword;
        MusicIdleTimeout = musicIdleTimeout;
        YoutubeLogin = youtubeLogin;
        YoutubeRefreshToken = youtubeRefreshToken;
        MalClientId = malClientId;
        Api = api;
        Ambiente = ambiente;
    }

    public string Token { get; }
    public string Prefix { get; }
    public ulong? DevGuildId { get; }
    public LogLevel LogLevel { get; }

    /// <summary>REST do servidor Lavalink; o WebSocket sai daqui sozinho.</summary>
    public Uri LavalinkUri { get; }

    public string LavalinkPassword { get; }

    /// <summary>Quanto tempo parado — sem ninguém no canal ou sem tocar — antes de eu sair.</summary>
    public TimeSpan MusicIdleTimeout { get; }

    /// <summary>
    /// Pede o login do YouTube no console durante o boot. Só faz sentido em servidor, onde
    /// o YouTube barra o IP; em casa a música toca sem isso.
    /// </summary>
    public bool YoutubeLogin { get; }

    /// <summary>
    /// Semente do token do YouTube: vale no primeiro boot, ou quando alguém cola um token
    /// novo no .env. Depois disso quem guarda o token em uso é o <c>YoutubeTokenStore</c>,
    /// porque o Google pode trocar ele sem avisar, e o .env não acompanharia.
    /// </summary>
    public string? YoutubeRefreshToken { get; }

    /// <summary>
    /// Client ID da API oficial do MyAnimeList. Opcional: sem ele o /ma pergunta ao Jikan,
    /// que não pede chave mas só enxerga o MAL quando o MAL deixa. Com ele a busca vai
    /// direto na fonte.
    /// </summary>
    public string? MalClientId { get; }

    /// <summary>
    /// A API HTTP de status. Null quando não há API_KEY: aí nenhuma porta abre — API sem
    /// chave seria o bot contando a vida dele pra quem passar na rua.
    /// </summary>
    public ApiConfig? Api { get; }

    /// <summary>O DOTNET_ENVIRONMENT: o compose põe "Production"; rodando em casa, fica vazio.</summary>
    public string? Ambiente { get; }

    public bool IsProduction => string.Equals(Ambiente, "Production", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Dev é quem tem DEV_GUILD_ID e não está em produção. O ambiente vence: um DEV_GUILD_ID
    /// esquecido no .env do servidor não faz o bot se anunciar como dev no banner, no log e
    /// no /api/status.
    /// </summary>
    public bool IsDevelopment => DevGuildId is not null && !IsProduction;

    private const string EnvFile = "Config/.env";

    // o padrão do Lavalink, pra quem sobe o application.yml daqui e não mexe em nada
    private const string LavalinkPadrão = "http://localhost:2333/";
    private const string SenhaPadrão = "youshallnotpass";

    private const int PortaDaApiPadrão = 8080;

    /// <summary>
    /// Abaixo disso a chave cai pra força bruta. A porta pode acabar aberta na internet, e
    /// quem bate nela não tem pressa.
    /// </summary>
    private const int ChaveMínima = 16;

    public static AlephConfig Load()
    {
        EnvLoader.Load(EnvFile);

        // sem TOKEN e sem arquivo: deixo o template pronto e paro, dizendo o que fazer
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TOKEN")))
        {
            var created = EnvLoader.CreateIfMissing(EnvFile);

            if (created is not null)
            {
                throw new InvalidOperationException(
                    $"Criei um .env de exemplo em '{created}'. Preencha o TOKEN e rode o bot de novo.");
            }
        }

        return De(Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// Monta a config a partir de uma fonte de variáveis: o ambiente do processo no boot, um
    /// dicionário nos testes — que assim não dependem do .env de quem roda nem mexem no
    /// ambiente uns dos outros.
    /// </summary>
    internal static AlephConfig De(Func<string, string?> variável)
    {
        var env = new Variáveis(variável);

        return new AlephConfig(
            token: env.Required("TOKEN"),
            prefix: env.Optional("PREFIX", "!"),
            devGuildId: env.OptionalUlong("DEV_GUILD_ID"),
            logLevel: env.OptionalEnum("LOG_LEVEL", LogLevel.Information),
            lavalinkUri: env.OptionalUri("LAVALINK_URI", LavalinkPadrão),
            lavalinkPassword: env.Optional("LAVALINK_PASSWORD", SenhaPadrão),
            musicIdleTimeout: env.OptionalMinutes("MUSIC_IDLE_MINUTES", TimeSpan.FromMinutes(2)),
            youtubeLogin: env.OptionalBool("YOUTUBE_LOGIN"),
            youtubeRefreshToken: env.OptionalOuNulo("YOUTUBE_REFRESH_TOKEN"),
            malClientId: env.OptionalOuNulo("MAL_CLIENT_ID"),
            api: env.OptionalApi(),
            ambiente: env.OptionalOuNulo("DOTNET_ENVIRONMENT"));
    }

    /// <summary>Lê cada variável da fonte e converte; o que vem torto vira erro com o nome dela.</summary>
    private sealed class Variáveis(Func<string, string?> ler)
    {
        /// <summary>
        /// Sem API_KEY a API não existe. Chave curta é erro, não aviso: subir com ela seria
        /// trancar a porta com um barbante e não contar pra ninguém.
        /// </summary>
        public ApiConfig? OptionalApi()
        {
            if (OptionalOuNulo("API_KEY") is not { } chave)
                return null;

            if (chave.Length < ChaveMínima)
            {
                throw new InvalidOperationException(
                    $"API_KEY curta demais ({chave.Length} caracteres): use pelo menos {ChaveMínima}. " +
                    "`openssl rand -hex 32` gera uma boa.");
            }

            return new ApiConfig(
                chave,
                OptionalPort("API_PORT", PortaDaApiPadrão),
                OptionalOuNulo("SCALAR_AGENT_KEY"),
                OptionalOuNulo("API_DOCS_PASSWORD"));
        }

        public string Required(string key)
        {
            var value = ler(key);

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Variável de ambiente obrigatória ausente: {key}");

            return value;
        }

        public string Optional(string key, string fallback)
        {
            var value = ler(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        /// <summary>Aceita o que alguém escreveria sem pensar: 1, true, yes, sim, on.</summary>
        public bool OptionalBool(string key) =>
            ler(key)?.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "sim" or "on";

        /// <summary>Vazio e ausente são a mesma coisa aqui: os dois querem dizer "não tenho".</summary>
        public string? OptionalOuNulo(string key)
        {
            var value = ler(key);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public ulong? OptionalUlong(string key) =>
            ulong.TryParse(ler(key), out var parsed) ? parsed : null;

        public TEnum OptionalEnum<TEnum>(string key, TEnum fallback) where TEnum : struct, Enum =>
            Enum.TryParse<TEnum>(ler(key), ignoreCase: true, out var parsed)
                ? parsed
                : fallback;

        /// <summary>
        /// Endereço torto no .env é erro, não motivo pra cair no padrão calado: quem trocou
        /// quer saber que errou, não descobrir depois de um boot inteiro batendo em localhost.
        /// </summary>
        public Uri OptionalUri(string key, string fallback)
        {
            var value = Optional(key, fallback);

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"{key} não é um endereço válido: '{value}'");

            return uri;
        }

        public TimeSpan OptionalMinutes(string key, TimeSpan fallback) =>
            double.TryParse(ler(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var minutos) && minutos > 0
                ? TimeSpan.FromMinutes(minutos)
                : fallback;

        /// <summary>Porta torta é erro pelo mesmo motivo do endereço torto: cair no padrão calado esconde o engano.</summary>
        public int OptionalPort(string key, int fallback)
        {
            var value = ler(key);

            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var porta) || porta is < 1 or > 65535)
                throw new InvalidOperationException($"{key} não é uma porta válida: '{value}'");

            return porta;
        }
    }
}

/// <summary>
/// Como a API escuta. Classe e não record de propósito: o ToString de um record imprimiria a
/// chave, e um log descuidado da config viraria vazamento.
/// </summary>
public sealed class ApiConfig
{
    public ApiConfig(string key, int port, string? scalarAgentKey = null, string? docsPassword = null)
    {
        Key = key;
        Port = port;
        ScalarAgentKey = scalarAgentKey;
        DocsPassword = docsPassword;
    }

    /// <summary>O que todo pedido tem que trazer no header X-Api-Key.</summary>
    public string Key { get; }

    public int Port { get; }

    /// <summary>
    /// A chave da IA do Scalar nos docs (/api/v1/docs). Sem ela a IA só aparece abrindo pelo
    /// localhost, que é onde o Scalar dá a cota grátis.
    /// </summary>
    public string? ScalarAgentKey { get; }

    /// <summary>
    /// Senha dos docs (/api/v1/docs) pra quem vem de fora (Funnel ou IP público). A página leva
    /// a chave da IA do Scalar no HTML, e sem senha qualquer um com o link usa as suas
    /// mensagens. Null = página aberta; de dentro ela nunca é pedida.
    /// </summary>
    public string? DocsPassword { get; }
}

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
        Uri? youtubeAudioUri,
        string? malClientId,
        ApiConfig? api,
        string? ambiente,
        string? databaseUrl)
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
        YoutubeAudioUri = youtubeAudioUri;
        MalClientId = malClientId;
        Api = api;
        Ambiente = ambiente;
        DatabaseUrl = databaseUrl;
    }

    public string Token { get; }
    public string Prefix { get; }
    public ulong? DevGuildId { get; }
    public LogLevel LogLevel { get; }

    // REST do servidor Lavalink; o WebSocket sai daqui sozinho.
    public Uri LavalinkUri { get; }

    public string LavalinkPassword { get; }

    // Quanto tempo parado — sem ninguém no canal ou sem tocar — antes de eu sair.
    public TimeSpan MusicIdleTimeout { get; }

    // Pede o login do YouTube no boot. Só serve em servidor, onde o YouTube barra o IP.
    public bool YoutubeLogin { get; }

    // Semente do token do YouTube: vale no primeiro boot. Depois quem guarda é o YoutubeTokenStore.
    public string? YoutubeRefreshToken { get; }

    // O yt-audio do compose, que pega o áudio com yt-dlp. null deixa tudo com o plugin do Lavalink.
    public Uri? YoutubeAudioUri { get; }

    // Client ID da API oficial do MAL. Sem ele o /ma vai no AniList, que não pede chave.
    public string? MalClientId { get; }

    // A API HTTP de status. Null sem API_KEY: API sem chave seria o bot contando a vida na rua.
    public ApiConfig? Api { get; }

    // Como chegar no Postgres. Null = subo sem banco; só o que guarda estado fica de fora.
    public string? DatabaseUrl { get; }

    // O DOTNET_ENVIRONMENT: o compose põe "Production"; rodando em casa, fica vazio.
    public string? Ambiente { get; }

    public bool IsProduction => string.Equals(Ambiente, "Production", StringComparison.OrdinalIgnoreCase);

    // Dev é quem tem DEV_GUILD_ID e não está em produção — o ambiente vence.
    public bool IsDevelopment => DevGuildId is not null && !IsProduction;

    private const string EnvFile = "Config/.env";

    // o padrão do Lavalink, pra quem sobe o application.yml daqui e não mexe em nada
    private const string LavalinkPadrão = "http://localhost:2333/";
    private const string SenhaPadrão = "youshallnotpass";

    private const int PortaDaApiPadrão = 8080;

    // Abaixo disso a chave cai pra força bruta, e quem bate na porta não tem pressa.
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

    // Monta a config de uma fonte de variáveis: o ambiente no boot, um dicionário nos testes.
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
            youtubeAudioUri: env.OptionalUriOuNulo("YOUTUBE_AUDIO_URI"),
            malClientId: env.OptionalOuNulo("MAL_CLIENT_ID"),
            api: env.OptionalApi(),
            ambiente: env.OptionalOuNulo("DOTNET_ENVIRONMENT"),
            databaseUrl: env.OptionalOuNulo("DATABASE_URL"));
    }

    // Lê cada variável da fonte e converte; o que vem torto vira erro com o nome dela.
    private sealed class Variáveis(Func<string, string?> ler)
    {
        // Sem API_KEY não há API. Chave curta é erro: trancar a porta com barbante é pior que nada.
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
                OptionalOuNulo("API_DOCS_PASSWORD"),
                Lista("API_CORS_ORIGINS"),
                OptionalOuNulo("API_DOCS_ADMIN_PASSWORD"));
        }

        // Uma variável com vários valores, separados por vírgula.
        private IReadOnlyList<string> Lista(string key) =>
            OptionalOuNulo(key) is { } valor
                ? [.. valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
                : [];

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

        // Aceita o que alguém escreveria sem pensar: 1, true, yes, sim, on.
        public bool OptionalBool(string key) =>
            ler(key)?.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "sim" or "on";

        // Vazio e ausente são a mesma coisa aqui: os dois querem dizer "não tenho".
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

        // Endereço torto é erro, não motivo pra cair no padrão calado.
        public Uri OptionalUri(string key, string fallback)
        {
            var value = Optional(key, fallback);

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"{key} não é um endereço válido: '{value}'");

            return uri;
        }

        // Endereço que pode faltar; torto continua sendo erro, pelo mesmo motivo do de cima.
        public Uri? OptionalUriOuNulo(string key)
        {
            if (OptionalOuNulo(key) is not { } value)
                return null;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"{key} não é um endereço válido: '{value}'");

            return uri;
        }

        public TimeSpan OptionalMinutes(string key, TimeSpan fallback) =>
            double.TryParse(ler(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var minutos) && minutos > 0
                ? TimeSpan.FromMinutes(minutos)
                : fallback;

        // Porta torta é erro pelo mesmo motivo do endereço torto.
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

// Como a API escuta. Classe e não record: o ToString de record imprimiria a chave no log.
public sealed class ApiConfig
{
    public ApiConfig(
        string key,
        int port,
        string? scalarAgentKey = null,
        string? docsPassword = null,
        IReadOnlyList<string>? corsOrigins = null,
        string? docsAdminPassword = null)
    {
        Key = key;
        Port = port;
        ScalarAgentKey = scalarAgentKey;
        DocsPassword = docsPassword;
        CorsOrigins = corsOrigins ?? [];
        DocsAdminPassword = docsAdminPassword;
    }

    // O que todo pedido tem que trazer no header X-Api-Key.
    public string Key { get; }

    public int Port { get; }

    // A chave da IA do Scalar nos docs. Sem ela a IA só aparece pelo localhost.
    public string? ScalarAgentKey { get; }

    // Senha dos docs pra quem vem de fora: a página leva a chave da IA no HTML. Null = aberta.
    public string? DocsPassword { get; }

    // Páginas a mais que podem chamar a API (API_CORS_ORIGINS); um "*" libera qualquer uma.
    public IReadOnlyList<string> CorsOrigins { get; }

    // A senha de dono dos docs: de fora, abre a página inteira, com a IA. Null = ninguém como dono.
    public string? DocsAdminPassword { get; }
}

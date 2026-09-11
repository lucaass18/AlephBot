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
        string? youtubeRefreshToken)
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

    public bool IsDevelopment => DevGuildId is not null;

    private const string EnvFile = "Config/.env";

    // o padrão do Lavalink, pra quem sobe o application.yml daqui e não mexe em nada
    private const string LavalinkPadrão = "http://localhost:2333/";
    private const string SenhaPadrão = "youshallnotpass";

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

        return new AlephConfig(
            token: Required("TOKEN"),
            prefix: Optional("PREFIX", "!"),
            devGuildId: OptionalUlong("DEV_GUILD_ID"),
            logLevel: OptionalEnum("LOG_LEVEL", LogLevel.Information),
            lavalinkUri: OptionalUri("LAVALINK_URI", LavalinkPadrão),
            lavalinkPassword: Optional("LAVALINK_PASSWORD", SenhaPadrão),
            musicIdleTimeout: OptionalMinutes("MUSIC_IDLE_MINUTES", TimeSpan.FromMinutes(2)),
            youtubeLogin: OptionalBool("YOUTUBE_LOGIN"),
            youtubeRefreshToken: OptionalOuNulo("YOUTUBE_REFRESH_TOKEN"));
    }

    private static string Required(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Variável de ambiente obrigatória ausente: {key}");

        return value;
    }

    private static string Optional(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>Aceita o que alguém escreveria sem pensar: 1, true, yes, sim, on.</summary>
    private static bool OptionalBool(string key) =>
        Environment.GetEnvironmentVariable(key)?.Trim().ToLowerInvariant()
            is "1" or "true" or "yes" or "sim" or "on";

    /// <summary>Vazio e ausente são a mesma coisa aqui: os dois querem dizer "não tenho".</summary>
    private static string? OptionalOuNulo(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static ulong? OptionalUlong(string key) =>
        ulong.TryParse(Environment.GetEnvironmentVariable(key), out var parsed) ? parsed : null;

    private static TEnum OptionalEnum<TEnum>(string key, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(Environment.GetEnvironmentVariable(key), ignoreCase: true, out var parsed)
            ? parsed
            : fallback;

    /// <summary>
    /// Endereço torto no .env é erro, não motivo pra cair no padrão calado: quem trocou
    /// quer saber que errou, não descobrir depois de um boot inteiro batendo em localhost.
    /// </summary>
    private static Uri OptionalUri(string key, string fallback)
    {
        var value = Optional(key, fallback);

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"{key} não é um endereço válido: '{value}'");

        return uri;
    }

    private static TimeSpan OptionalMinutes(string key, TimeSpan fallback) =>
        double.TryParse(Environment.GetEnvironmentVariable(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var minutos) && minutos > 0
            ? TimeSpan.FromMinutes(minutos)
            : fallback;
}

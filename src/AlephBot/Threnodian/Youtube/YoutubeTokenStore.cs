using System.Text.Json;
using System.Text.Json.Serialization;

using AlephBot.Config;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Youtube;

// Onde o refresh token mora entre um boot e outro: o arquivo é a verdade, o .env é a semente.
public sealed class YoutubeTokenStore
{
    private static readonly JsonSerializerOptions Formato = new() { WriteIndented = true };

    private readonly string _caminho;
    private readonly string? _semente;
    private readonly ILogger<YoutubeTokenStore> _logger;

    // a semente que gerou o arquivo atual; se o .env mudar, é sinal de token novo colado lá
    private string? _sementeUsada;

    public YoutubeTokenStore(AlephConfig config, ILogger<YoutubeTokenStore> logger)
    {
        // no container é o volume aleph-data; em casa, a pasta do build
        _caminho = Path.Combine(AppContext.BaseDirectory, "data", "youtube-token.json");
        _semente = config.YoutubeRefreshToken;
        _logger = logger;
    }

    // O token em uso, ou null quando ninguém fez login ainda.
    public string? Token { get; private set; }

    public string Caminho => _caminho;

    public void Carregar()
    {
        var salvo = Ler();

        // token no .env diferente do que semeou o arquivo: alguém colou um novo, e o novo ganha
        if (_semente is not null && salvo?.Semente != _semente)
        {
            _sementeUsada = _semente;
            Guardar(_semente);
            return;
        }

        _sementeUsada = salvo?.Semente;
        Token = salvo?.Token;
    }

    // Troca o token em uso e grava. Falha de disco não derruba nada: fica em memória.
    public void Guardar(string token)
    {
        Token = token;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_caminho)!);
            File.WriteAllText(_caminho, JsonSerializer.Serialize(new Guardado(token, _sementeUsada), Formato));
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(erro, "Não consegui gravar o token do YouTube em {Caminho}; ele vale só até eu desligar", _caminho);
        }
    }

    private Guardado? Ler()
    {
        try
        {
            if (!File.Exists(_caminho))
                return null;

            var guardado = JsonSerializer.Deserialize<Guardado>(File.ReadAllText(_caminho));

            return string.IsNullOrWhiteSpace(guardado?.Token) ? null : guardado;
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(erro, "Não consegui ler o token do YouTube em {Caminho}", _caminho);
            return null;
        }
    }

    private sealed record Guardado(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("semente")] string? Semente);
}

using System.Text.Json;
using System.Text.Json.Serialization;

using Lavalink4NET.Players.Queued;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Players;

/// <summary>Uma faixa como ela é guardada: os dados codificados do Lavalink e quem pediu.</summary>
public sealed record FaixaGuardada(string Dados, string? QuemPediu);

/// <summary>
/// Tudo que um player precisa pra voltar a ser o que era depois de um restart: onde
/// estava, o que tocava, até onde foi, e o que vinha depois.
/// </summary>
public sealed record PlayerSnapshot(
    ulong GuildId,
    ulong CanalDeVoz,
    ulong? CanalDeTexto,
    FaixaGuardada? Atual,
    TimeSpan Posição,
    bool Pausado,
    IReadOnlyList<FaixaGuardada> Fila,
    float Volume,
    TrackRepeatMode Repetição,
    DateTimeOffset SalvoEm);

/// <summary>
/// Onde os players moram enquanto eu não existo.
///
/// A fila vive na minha memória, não no Lavalink: quando eu reinicio, ela some junto. Aqui
/// eu guardo o suficiente pra refazer cada player na volta — e quem escreve é o próprio
/// player, a cada mudança, então o arquivo acompanha o que está tocando sem ninguém pedir.
/// </summary>
public sealed class PlayerSnapshotStore
{
    private static readonly JsonSerializerOptions Formato = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _caminho;
    private readonly ILogger<PlayerSnapshotStore> _logger;
    private readonly Lock _trava = new();

    private readonly Dictionary<ulong, PlayerSnapshot> _fotos = [];

    // no desligamento os players são destruídos, e cada um tentaria apagar a própria foto —
    // justamente a que eu acabei de tirar pra retomar depois. congelado, o arquivo não mexe
    private bool _congelado;

    public PlayerSnapshotStore(ILogger<PlayerSnapshotStore> logger)
    {
        // no container é o volume aleph-data; em casa, a pasta do build
        _caminho = Path.Combine(AppContext.BaseDirectory, "data", "players.json");
        _logger = logger;
    }

    /// <summary>Lê o que ficou do boot anterior. Chamado uma vez, na volta.</summary>
    public IReadOnlyList<PlayerSnapshot> Carregar()
    {
        lock (_trava)
        {
            _fotos.Clear();

            try
            {
                if (File.Exists(_caminho))
                {
                    var lidas = JsonSerializer.Deserialize<List<PlayerSnapshot>>(File.ReadAllText(_caminho), Formato) ?? [];

                    foreach (var foto in lidas)
                        _fotos[foto.GuildId] = foto;
                }
            }
            catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogWarning(erro, "Não consegui ler os players guardados em {Caminho}", _caminho);
            }

            return [.. _fotos.Values];
        }
    }

    public void Guardar(PlayerSnapshot foto)
    {
        lock (_trava)
        {
            if (_congelado)
                return;

            _fotos[foto.GuildId] = foto;
            Escrever();
        }
    }

    /// <summary>O player saiu do canal por vontade própria: não tem o que retomar.</summary>
    public void Esquecer(ulong guildId)
    {
        lock (_trava)
        {
            if (_congelado || !_fotos.Remove(guildId))
                return;

            Escrever();
        }
    }

    /// <summary>Depois daqui nada mais muda o arquivo: é o que o próximo boot vai ler.</summary>
    public void Congelar()
    {
        lock (_trava)
        {
            _congelado = true;
        }
    }

    private void Escrever()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_caminho)!);

            // escrevo do lado e troco: um crash no meio da escrita não pode deixar meio JSON
            var temporário = _caminho + ".tmp";
            File.WriteAllText(temporário, JsonSerializer.Serialize(_fotos.Values, Formato));
            File.Move(temporário, _caminho, overwrite: true);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(erro, "Não consegui guardar os players em {Caminho}", _caminho);
        }
    }
}

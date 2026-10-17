using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

using AlephBot.Config;
using AlephBot.Threnodian.Rede;

using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Youtube;

// O áudio do YouTube pelo yt-audio do compose: de servidor o plugin do Lavalink não consegue.
public sealed partial class YoutubeAudio : IDisposable
{
    // Quantos vídeos eu guardo antes de recomeçar as listas.
    private const int Guardados = 500;

    // O primeiro pedido roda o yt-dlp inteiro — player, desafio no deno, token. Leva segundos.
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(60);

    // O canal de cada vídeo, que o yt-dlp já acha no /preparar; assim o embed não pergunta nada.
    private readonly ConcurrentDictionary<string, string> _canais = new();

    // A faixa que o Lavalink já carregou de cada vídeo. O /v/<id> não muda, e carregar de novo
    // é outra ida ao YouTube — a cada troca de faixa — só pra redescobrir o mesmo formato.
    private readonly ConcurrentDictionary<string, Task<LavalinkTrack?>> _prontas = new();

    private readonly Uri? _base;
    private readonly Func<string, CancellationToken, ValueTask<LavalinkTrack?>> _carregar;
    private readonly HttpClient _http;
    private readonly ILogger<YoutubeAudio> _logger;

    public YoutubeAudio(AlephConfig config, IAudioService áudio, ILogger<YoutubeAudio> logger)
        : this(
            config.YoutubeAudioUri,
            (endereço, cancellationToken) => áudio.Tracks.LoadTrackAsync(
                endereço, TrackSearchMode.None, cancellationToken: cancellationToken),
            ClienteHttp.Novo(Espera),
            logger)
    {
    }

    // Os testes entram por aqui: o Lavalink vira função e o yt-audio, um handler de mentira.
    internal YoutubeAudio(
        Uri? @base,
        Func<string, CancellationToken, ValueTask<LavalinkTrack?>> carregar,
        HttpClient http,
        ILogger<YoutubeAudio> logger)
    {
        // sem a barra no fim, o Uri trocaria o último pedaço do caminho em vez de somar
        _base = @base is null || @base.AbsoluteUri.EndsWith('/') ? @base : new Uri(@base.AbsoluteUri + "/");
        _carregar = carregar;
        _http = http;
        _logger = logger;
    }

    // A faixa que toca pelo yt-audio, ou ela mesma quando não é caso (ao vivo, outra fonte).
    public LavalinkTrack Envolver(LavalinkTrack faixa) =>
        Serve(faixa) ? new FaixaDoYoutube(faixa, this) : faixa;

    internal bool Serve(LavalinkTrack faixa) =>
        _base is not null
        && faixa is not FaixaDoYoutube
        && string.Equals(faixa.SourceName, "youtube", StringComparison.OrdinalIgnoreCase)
        && !faixa.IsLiveStream
        && IdDeVídeo().IsMatch(faixa.Identifier);

    // Pede o link da próxima antes da vez, pra ela começar sem o silêncio do yt-dlp.
    public void Preparar(LavalinkTrack? faixa)
    {
        if (faixa is FaixaDoYoutube doYoutube)
            _ = AdiantarAsync(doYoutube);
    }

    // As duas esperas da próxima faixa, fora da hora dela: o yt-dlp resolve o link e o Lavalink
    // já carrega ele. Na vez dela não sobra nenhuma das duas.
    private async Task AdiantarAsync(FaixaDoYoutube faixa)
    {
        if (await PrepararAsync(faixa, CancellationToken.None))
            await ProntaAsync(faixa);
    }

    // O yt-audio prepara o link e o Lavalink carrega ele pronto; falha devolve a faixa original.
    internal async ValueTask<LavalinkTrack> TocávelAsync(FaixaDoYoutube faixa, CancellationToken cancellationToken)
    {
        var relógio = Stopwatch.StartNew();

        // o /preparar é sempre de agora: o link que o yt-audio guarda vence, e quem abre o áudio
        // depois é o Lavalink, que não sabe esperar o yt-dlp
        if (!await PrepararAsync(faixa, cancellationToken))
            return faixa.Original;

        if (await ProntaAsync(faixa).WaitAsync(cancellationToken) is not { } http)
            return faixa.Original;

        _logger.LogDebug(
            "O áudio de {Faixa} ({Id}) ficou pronto em {Tempo} ms",
            faixa.Title, faixa.Identifier, relógio.ElapsedMilliseconds);

        return http;
    }

    // A faixa do /v/<id>, carregada uma vez só — quem chega depois espera a mesma resposta.
    private async Task<LavalinkTrack?> ProntaAsync(FaixaDoYoutube faixa)
    {
        // o teto evita a memória crescer sozinha; o que sair volta no próximo /play
        if (_prontas.Count >= Guardados)
            _prontas.Clear();

        var tentativa = _prontas.GetOrAdd(faixa.Identifier, _ => DoLavalinkAsync(faixa));

        var pronta = await tentativa;

        // recusa não fica guardada, que o yt-audio pode estar só de pé num minuto ruim; tiro
        // esta tentativa, e não a chave, pra não levar junto uma que já começou de novo
        if (pronta is null)
            _prontas.TryRemove(KeyValuePair.Create(faixa.Identifier, tentativa));

        return pronta;
    }

    // O que o Lavalink respondeu, ou null quando ele não deu conta.
    private async Task<LavalinkTrack?> DoLavalinkAsync(FaixaDoYoutube faixa)
    {
        try
        {
            // sem cancelamento: a resposta é de todo mundo, e quem desistiu só não espera por ela
            if (await _carregar(Endereço("v", faixa.Identifier).AbsoluteUri, CancellationToken.None) is { } http)
                return http;

            _logger.LogWarning(
                "O Lavalink não carregou o áudio de {Faixa} ({Id}) pelo yt-audio; vai pelo plugin",
                faixa.Title, faixa.Identifier);
        }
        catch (Exception erro)
        {
            _logger.LogWarning(
                erro, "O Lavalink não carregou o áudio de {Faixa} ({Id}) pelo yt-audio; vai pelo plugin",
                faixa.Title, faixa.Identifier);
        }

        return null;
    }

    // O canal de quem publicou, quando o yt-audio já contou qual é.
    internal string? CanalDe(string id) =>
        _canais.TryGetValue(id, out var canal) ? canal : null;

    // O yt-audio descobre e guarda o link. True quando ele conseguiu.
    private async Task<bool> PrepararAsync(FaixaDoYoutube faixa, CancellationToken cancellationToken)
    {
        try
        {
            using var resposta = await _http.GetAsync(Endereço("preparar", faixa.Identifier), cancellationToken);

            if (resposta.IsSuccessStatusCode)
            {
                await AnotarCanalAsync(faixa.Identifier, resposta, cancellationToken);
                return true;
            }

            // o yt-audio responde com o motivo do yt-dlp — "Sign in to confirm you're not a bot"
            var motivo = (await resposta.Content.ReadAsStringAsync(cancellationToken)).Trim();

            _logger.LogWarning(
                "O yt-audio recusou {Faixa} ({Id}): {Status} {Motivo}",
                faixa.Title, faixa.Identifier, (int)resposta.StatusCode, motivo);
        }
        catch (Exception erro) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(erro, "O yt-audio não respondeu por {Faixa} ({Id})", faixa.Title, faixa.Identifier);
        }

        return false;
    }

    // Guarda o canal que veio no /preparar; sem ele, o embed só não linka o artista.
    private async Task AnotarCanalAsync(string id, HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            using var documento = JsonDocument.Parse(corpo);

            if (documento.RootElement.TryGetProperty("canal", out var canal)
                && canal.ValueKind is JsonValueKind.String
                && canal.GetString() is { Length: > 0 } endereço)
            {
                // o teto evita a memória crescer sozinha; o que sair volta no próximo /preparar
                if (_canais.Count >= Guardados)
                    _canais.Clear();

                _canais[id] = endereço;
            }
        }
        catch (Exception erro) when (erro is JsonException or HttpRequestException)
        {
            _logger.LogDebug(erro, "O yt-audio respondeu o /preparar de {Id} sem o canal", id);
        }
    }

    internal Uri Endereço(string rota, string id) => new(_base!, $"{rota}/{id}");

    // O id de um vídeo do YouTube: 11 caracteres do alfabeto base64 de URL.
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdDeVídeo();

    public void Dispose() => _http.Dispose();
}

// Uma faixa do YouTube que toca pelo yt-audio: pro resto do bot é a faixa de sempre.
public sealed record FaixaDoYoutube : LavalinkTrack
{
    private readonly YoutubeAudio _áudio;

    [SetsRequiredMembers]
    internal FaixaDoYoutube(LavalinkTrack original, YoutubeAudio áudio)
        : base(original)
    {
        Original = original;
        _áudio = áudio;
    }

    // A faixa como o plugin entregou: é ela que toca quando o yt-audio falha.
    public LavalinkTrack Original { get; }

    // O canal de quem publicou, ou null enquanto o yt-audio ainda não falou dela.
    public string? Canal => _áudio.CanalDe(Identifier);

    public override ValueTask<LavalinkTrack> GetPlayableTrackAsync(CancellationToken cancellationToken = default) =>
        _áudio.TocávelAsync(this, cancellationToken);

    // O texto codificado da original: é ele que vai pra foto, e o Lavalink lê o que escreveu.
    public override string ToString() => Original.ToString();
}

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

using AlephBot.Config;

using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Youtube;

/// <summary>
/// O áudio do YouTube pelo yt-audio, o serviço do compose que pega o áudio com o yt-dlp, sem
/// conta. De um servidor o YouTube não entrega mais o áudio pro plugin do Lavalink sem login;
/// pro yt-audio, que sai pelo WARP com um token anti-robô, entrega.
///
/// A faixa continua sendo a do plugin — título, capa, link, fila, a foto do restart. Só muda
/// o que o Lavalink toca: no lugar da faixa do YouTube, o link HTTP do yt-audio. Se ele
/// falhar, a faixa vai como sempre foi, pelo plugin (e pelo login, se houver).
/// </summary>
public sealed partial class YoutubeAudio : IDisposable
{
    /// <summary>Quantos canais eu guardo antes de recomeçar a lista.</summary>
    private const int CanaisGuardados = 500;

    /// <summary>
    /// O primeiro pedido de uma música faz o yt-audio rodar o yt-dlp inteiro — o player do
    /// YouTube, o desafio dele no deno, o token. Leva segundos; no boot do contêiner, mais.
    /// </summary>
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(60);

    /// <summary>
    /// O canal de quem publicou cada vídeo, pelo id. Quem descobre é o yt-dlp, lá no
    /// yt-audio, enquanto acha o áudio — então vem de graça junto do /preparar, e fica aqui
    /// pro embed não ter que perguntar nada ao YouTube na hora de montar a ficha.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _canais = new();

    private readonly Uri? _base;
    private readonly Func<string, CancellationToken, ValueTask<LavalinkTrack?>> _carregar;
    private readonly HttpClient _http;
    private readonly ILogger<YoutubeAudio> _logger;

    public YoutubeAudio(AlephConfig config, IAudioService áudio, ILogger<YoutubeAudio> logger)
        : this(
            config.YoutubeAudioUri,
            (endereço, cancellationToken) => áudio.Tracks.LoadTrackAsync(
                endereço, TrackSearchMode.None, cancellationToken: cancellationToken),
            new HttpClient { Timeout = Espera },
            logger)
    {
    }

    /// <summary>
    /// Os testes entram por aqui: o Lavalink vira uma função e o yt-audio, um handler HTTP
    /// de mentira.
    /// </summary>
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

    /// <summary>
    /// A faixa que toca pelo yt-audio, ou ela mesma quando não é caso: yt-audio desligado,
    /// faixa de outra fonte, transmissão ao vivo (o yt-audio só serve arquivo) ou já envolvida.
    /// </summary>
    public LavalinkTrack Envolver(LavalinkTrack faixa) =>
        Serve(faixa) ? new FaixaDoYoutube(faixa, this) : faixa;

    internal bool Serve(LavalinkTrack faixa) =>
        _base is not null
        && faixa is not FaixaDoYoutube
        && string.Equals(faixa.SourceName, "youtube", StringComparison.OrdinalIgnoreCase)
        && !faixa.IsLiveStream
        && IdDeVídeo().IsMatch(faixa.Identifier);

    /// <summary>
    /// Pede pro yt-audio já descobrir o link de uma faixa que ainda vai tocar — a próxima da
    /// fila —, pra ela começar sem o silêncio do yt-dlp. Não espera: se falhar, ela só começa
    /// mais devagar.
    /// </summary>
    public void Preparar(LavalinkTrack? faixa)
    {
        if (faixa is FaixaDoYoutube doYoutube)
            _ = PrepararAsync(doYoutube, CancellationToken.None);
    }

    /// <summary>
    /// O que o Lavalink toca no lugar da faixa: primeiro o yt-audio prepara o link (é a parte
    /// demorada, e assim ela não esbarra no tempo de leitura do Lavalink); depois o Lavalink
    /// carrega o link pronto. Qualquer falha devolve a faixa original, que vai pelo plugin.
    /// </summary>
    internal async ValueTask<LavalinkTrack> TocávelAsync(FaixaDoYoutube faixa, CancellationToken cancellationToken)
    {
        try
        {
            if (!await PrepararAsync(faixa, cancellationToken))
                return faixa.Original;

            if (await _carregar(Endereço("v", faixa.Identifier).AbsoluteUri, cancellationToken) is { } http)
                return http;

            _logger.LogWarning(
                "O Lavalink não carregou o áudio de {Faixa} ({Id}) pelo yt-audio; vai pelo plugin",
                faixa.Title, faixa.Identifier);
        }
        catch (Exception erro) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                erro, "O Lavalink não carregou o áudio de {Faixa} ({Id}) pelo yt-audio; vai pelo plugin",
                faixa.Title, faixa.Identifier);
        }

        return faixa.Original;
    }

    /// <summary>O canal de quem publicou, quando o yt-audio já contou qual é.</summary>
    internal string? CanalDe(string id) =>
        _canais.TryGetValue(id, out var canal) ? canal : null;

    /// <summary>O yt-audio descobre e guarda o link. True quando ele conseguiu.</summary>
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

    /// <summary>
    /// Guarda o canal que veio na resposta do /preparar. Falha nenhuma daqui atrapalha a
    /// música: sem o canal, o embed só deixa de pôr o link no nome do artista.
    /// </summary>
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
                // o teto evita a memória crescer sozinha num servidor que toca o dia inteiro;
                // o que cair fora volta no próximo /preparar da mesma faixa
                if (_canais.Count >= CanaisGuardados)
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

    /// <summary>O id de um vídeo do YouTube: 11 caracteres do alfabeto base64 de URL.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdDeVídeo();

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Uma faixa do YouTube que toca pelo yt-audio. Pro resto do bot é a faixa de sempre: título,
/// capa, link e o texto codificado são os da faixa do plugin. Só o que vai pro Lavalink na
/// hora de tocar sai do yt-audio — pelo gancho que a biblioteca oferece pra isso, o mesmo das
/// faixas do Spotify que tocam por outra fonte. A biblioteca guarda a faixa da fila como a
/// atual, e não o link tocado: o "tocando agora" segue mostrando o YouTube.
/// </summary>
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

    /// <summary>A faixa como o plugin entregou: é ela que toca quando o yt-audio falha.</summary>
    public LavalinkTrack Original { get; }

    /// <summary>
    /// O canal de quem publicou, ou null enquanto o yt-audio ainda não falou dela — é o que
    /// acontece com uma faixa que entrou na fila e nem foi preparada ainda.
    /// </summary>
    public string? Canal => _áudio.CanalDe(Identifier);

    public override ValueTask<LavalinkTrack> GetPlayableTrackAsync(CancellationToken cancellationToken = default) =>
        _áudio.TocávelAsync(this, cancellationToken);

    /// <summary>
    /// O texto codificado da faixa original, e não um feito de novo: é ele que vai pra foto do
    /// restart, e o Lavalink lê de volta exatamente o que ele mesmo escreveu. Sem isto o record
    /// geraria um ToString próprio, com nome de propriedade, e a retomada não decodificaria.
    /// </summary>
    public override string ToString() => Original.ToString();
}

using System.Net;

using AlephBot.Threnodian.Youtube;

using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging.Abstractions;

namespace AlephBot.Tests.Youtube;

/// <summary>
/// A faixa do YouTube tocando pelo yt-audio: pro bot ela é a faixa de sempre (o que aparece
/// no Discord e o que vai pra foto do restart), e qualquer falha do yt-audio devolve a faixa
/// do plugin em vez de deixar a música muda.
/// </summary>
public class YoutubeAudioTests
{
    private const string Id = "dQw4w9WgXcQ";

    private static readonly Uri YtAudio = new("http://yt-audio:8080/");

    private static LavalinkTrack DoYoutube(string id = Id, string fonte = "youtube", bool aoVivo = false) => new()
    {
        Title = "Never Gonna Give You Up",
        Author = "Rick Astley",
        Identifier = id,
        SourceName = fonte,
        Duration = TimeSpan.FromSeconds(213),
        IsSeekable = !aoVivo,
        IsLiveStream = aoVivo,
        Uri = new Uri($"https://www.youtube.com/watch?v={id}"),
        ArtworkUri = new Uri($"https://i.ytimg.com/vi/{id}/maxresdefault.jpg"),
    };

    private static readonly LavalinkTrack DoLink = new()
    {
        Title = "Unknown title",
        Author = "Unknown artist",
        Identifier = $"http://yt-audio:8080/v/{Id}",
        SourceName = "http",
        Duration = TimeSpan.FromSeconds(213),
        IsSeekable = true,
    };

    /// <summary>O Lavalink de mentira: guarda o que pediram pra ele carregar.</summary>
    private sealed class Lavalink(Func<string, LavalinkTrack?> responder)
    {
        public List<string> Carregados { get; } = [];

        public ValueTask<LavalinkTrack?> CarregarAsync(string endereço, CancellationToken cancellationToken)
        {
            Carregados.Add(endereço);
            return ValueTask.FromResult(responder(endereço));
        }
    }

    /// <summary>O yt-audio de mentira: responde ao /preparar com o status que o teste quiser.</summary>
    private sealed class YtAudioDeMentira(HttpStatusCode status = HttpStatusCode.NoContent, Exception? falha = null)
        : HttpMessageHandler
    {
        public List<Uri> Pedidos { get; } = [];

        public TaskCompletionSource<Uri> Primeiro { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // como o handler de verdade: pedido cancelado nem sai
            cancellationToken.ThrowIfCancellationRequested();

            lock (Pedidos)
                Pedidos.Add(request.RequestUri!);

            Primeiro.TrySetResult(request.RequestUri!);

            if (falha is not null)
                throw falha;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("Sign in to confirm you're not a bot"),
            });
        }
    }

    private static YoutubeAudio Montar(Uri? endereço, Lavalink lavalink, HttpMessageHandler ytAudio) =>
        new(endereço, lavalink.CarregarAsync, new HttpClient(ytAudio), NullLogger<YoutubeAudio>.Instance);

    [Fact]
    public void Faixa_do_YouTube_sai_envolvida_com_os_dados_dela()
    {
        var original = DoYoutube();
        var áudio = Montar(YtAudio, new Lavalink(_ => DoLink), new YtAudioDeMentira());

        var faixa = Assert.IsType<FaixaDoYoutube>(áudio.Envolver(original));

        // o "tocando agora", a fila e o embed leem daqui
        Assert.Same(original, faixa.Original);
        Assert.Equal(original.Title, faixa.Title);
        Assert.Equal(original.Author, faixa.Author);
        Assert.Equal(original.Identifier, faixa.Identifier);
        Assert.Equal(original.Duration, faixa.Duration);
        Assert.Equal(original.Uri, faixa.Uri);
        Assert.Equal(original.ArtworkUri, faixa.ArtworkUri);
        Assert.Equal("youtube", faixa.SourceName);
        Assert.True(faixa.IsSeekable);
    }

    [Fact]
    public void Sem_yt_audio_a_faixa_fica_como_veio()
    {
        var original = DoYoutube();
        var áudio = Montar(null, new Lavalink(_ => DoLink), new YtAudioDeMentira());

        Assert.Same(original, áudio.Envolver(original));
    }

    [Fact]
    public void Outra_fonte_ao_vivo_e_id_estranho_ficam_com_o_plugin()
    {
        var áudio = Montar(YtAudio, new Lavalink(_ => DoLink), new YtAudioDeMentira());

        foreach (var faixa in new[] { DoYoutube(fonte: "soundcloud"), DoYoutube(aoVivo: true), DoYoutube(id: "curto") })
            Assert.Same(faixa, áudio.Envolver(faixa));
    }

    [Fact]
    public void Envolver_de_novo_não_empilha()
    {
        var áudio = Montar(YtAudio, new Lavalink(_ => DoLink), new YtAudioDeMentira());
        var faixa = áudio.Envolver(DoYoutube());

        Assert.Same(faixa, áudio.Envolver(faixa));
    }

    [Fact]
    public void A_foto_do_restart_guarda_a_faixa_original()
    {
        var original = DoYoutube();
        var faixa = Montar(YtAudio, new Lavalink(_ => DoLink), new YtAudioDeMentira()).Envolver(original);

        Assert.Equal(original.ToString(), faixa.ToString());

        // é isso que a retomada lê de volta
        Assert.True(LavalinkTrack.TryParse(faixa.ToString(), null, out var lida));
        Assert.Equal(original.Title, lida.Title);
        Assert.Equal(original.Identifier, lida.Identifier);
        Assert.Equal("youtube", lida.SourceName);
    }

    [Fact]
    public async Task Na_hora_de_tocar_o_Lavalink_recebe_o_link_do_yt_audio()
    {
        var lavalink = new Lavalink(_ => DoLink);
        var ytAudio = new YtAudioDeMentira();
        var faixa = Montar(YtAudio, lavalink, ytAudio).Envolver(DoYoutube());

        var tocável = await faixa.GetPlayableTrackAsync(TestContext.Current.CancellationToken);

        Assert.Same(DoLink, tocável);
        // primeiro o yt-audio prepara (a parte demorada), depois o Lavalink carrega o link pronto
        Assert.Equal([new Uri($"http://yt-audio:8080/preparar/{Id}")], ytAudio.Pedidos);
        Assert.Equal([$"http://yt-audio:8080/v/{Id}"], lavalink.Carregados);
    }

    [Fact]
    public async Task Endereço_sem_barra_no_fim_também_serve()
    {
        var lavalink = new Lavalink(_ => DoLink);
        var faixa = Montar(new Uri("http://yt-audio:8080"), lavalink, new YtAudioDeMentira()).Envolver(DoYoutube());

        await faixa.GetPlayableTrackAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"http://yt-audio:8080/v/{Id}"], lavalink.Carregados);
    }

    [Fact]
    public async Task yt_audio_recusando_a_faixa_vai_pelo_plugin()
    {
        var original = DoYoutube();
        var lavalink = new Lavalink(_ => DoLink);
        var faixa = Montar(YtAudio, lavalink, new YtAudioDeMentira(HttpStatusCode.BadGateway)).Envolver(original);

        Assert.Same(original, await faixa.GetPlayableTrackAsync(TestContext.Current.CancellationToken));
        Assert.Empty(lavalink.Carregados);
    }

    [Fact]
    public async Task yt_audio_fora_do_ar_vai_pelo_plugin()
    {
        var original = DoYoutube();
        var ytAudio = new YtAudioDeMentira(falha: new HttpRequestException("Connection refused"));
        var faixa = Montar(YtAudio, new Lavalink(_ => DoLink), ytAudio).Envolver(original);

        Assert.Same(original, await faixa.GetPlayableTrackAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Lavalink_sem_carregar_o_link_vai_pelo_plugin()
    {
        var original = DoYoutube();
        var nada = Montar(YtAudio, new Lavalink(_ => null), new YtAudioDeMentira()).Envolver(original);
        var erro = Montar(YtAudio, new Lavalink(_ => throw new HttpRequestException("500")), new YtAudioDeMentira()).Envolver(original);

        Assert.Same(original, await nada.GetPlayableTrackAsync(TestContext.Current.CancellationToken));
        Assert.Same(original, await erro.GetPlayableTrackAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Desligando_o_cancelamento_não_vira_reserva()
    {
        using var cancelar = new CancellationTokenSource();
        await cancelar.CancelAsync();

        var faixa = Montar(YtAudio, new Lavalink(_ => DoLink), new YtAudioDeMentira()).Envolver(DoYoutube());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => faixa.GetPlayableTrackAsync(cancelar.Token).AsTask());
    }

    [Fact]
    public async Task Preparar_pede_ao_yt_audio_só_faixa_que_toca_por_ele()
    {
        var ytAudio = new YtAudioDeMentira();
        var áudio = Montar(YtAudio, new Lavalink(_ => DoLink), ytAudio);

        áudio.Preparar(null);
        áudio.Preparar(DoYoutube(fonte: "soundcloud"));
        áudio.Preparar(áudio.Envolver(DoYoutube()));

        var pedido = await ytAudio.Primeiro.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(new Uri($"http://yt-audio:8080/preparar/{Id}"), pedido);
        Assert.Single(ytAudio.Pedidos);
    }
}

using System.Net.WebSockets;

using AlephBot.Core.Commands.Music;

using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

namespace AlephBot.Tests.Commands;

// O que o módulo de música faz sem precisar de Lavalink: ler a entrada e formatar a saída.
public class MusicTests
{
    // ---- /seek ---------------------------------------------------------------

    [Theory]
    [InlineData("1:30", 90)]
    [InlineData("01:30", 90)]
    [InlineData("1:02:03", 3_723)]
    [InlineData(" 1:30 ", 90)]
    [InlineData("90", 90)]
    [InlineData("1m30s", 90)]
    [InlineData("2m", 120)]
    [InlineData("1h", 3_600)]
    [InlineData("0", 0)]
    public void Posição_aceita_relógio_segundos_e_unidades(string texto, int segundos)
    {
        Assert.Equal(TimeSpan.FromSeconds(segundos), Music.ParsePosição(texto));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1::30")]
    [InlineData("1:2:3:4")]
    [InlineData("a:b")]
    [InlineData("-1:30")]
    [InlineData("1:-30")]
    [InlineData("1x")]
    [InlineData("m")]
    [InlineData("99999999")]
    public void Posição_que_não_dá_pra_ler_é_null(string texto)
    {
        Assert.Null(Music.ParsePosição(texto));
    }

    // ---- formatação ----------------------------------------------------------

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(221, "3:41")]
    [InlineData(3_599, "59:59")]
    [InlineData(3_600, "1:00:00")]
    [InlineData(3_723, "1:02:03")]
    public void Duração_usa_hora_só_quando_precisa(int segundos, string esperado)
    {
        Assert.Equal(esperado, Music.Duração(TimeSpan.FromSeconds(segundos)));
    }

    [Theory]
    [InlineData(null, "?")]
    [InlineData("", "?")]
    [InlineData("a*b", @"a\*b")]
    public void Faixa_sem_título_vira_interrogação(string? título, string esperado)
    {
        Assert.Equal(esperado, Music.Escapa(título));
    }

    [Theory]
    [InlineData(0.5f, 50)]
    [InlineData(1f, 100)]
    [InlineData(0.333f, 33)]
    [InlineData(2f, 200)]
    public void Volume_vira_porcentagem(float volume, int porcento)
    {
        Assert.Equal(porcento, Music.Porcento(volume));
    }

    [Theory]
    [InlineData(TrackRepeatMode.Track, "🔂")]
    [InlineData(TrackRepeatMode.Queue, "🔁")]
    [InlineData(TrackRepeatMode.None, "➖")]
    public void Cada_modo_de_repetição_tem_seu_ícone(TrackRepeatMode modo, string ícone)
    {
        Assert.Equal(ícone, Music.Repetição(modo));
    }

    // ---- busca ---------------------------------------------------------------

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ&start_radio=1")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&start_radio=1")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?list=RDdQw4w9WgXcQ")]
    public void Link_de_rádio_vira_só_o_vídeo(string link)
    {
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", Music.SemRádio(link));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG")]
    [InlineData("https://www.youtube.com/playlist?list=RDdQw4w9WgXcQ")]
    [InlineData("never gonna give you up")]
    [InlineData("ftp://www.youtube.com/watch?v=x&list=RDx")]
    public void Playlist_de_verdade_e_texto_passam_como_vieram(string busca)
    {
        Assert.Equal(busca, Music.SemRádio(busca));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=x", true)]
    [InlineData("http://exemplo.com", true)]
    [InlineData("ftp://exemplo.com", false)]
    [InlineData("never gonna give you up", false)]
    public void Só_http_e_https_contam_como_link(string texto, bool éLink)
    {
        Assert.Equal(éLink, Music.ÉLink(texto));
    }

    [Fact]
    public void Link_vai_cru_e_texto_vira_busca_no_YouTube()
    {
        Assert.Equal(TrackSearchMode.None, Music.ModoDeBusca("https://www.youtube.com/watch?v=x"));
        Assert.Equal(TrackSearchMode.YouTube, Music.ModoDeBusca("never gonna give you up"));
    }

    // ---- reserva no SoundCloud -------------------------------------------------

    private static readonly LavalinkTrack DoSoundCloud = new()
    {
        Title = "Never Gonna Give You Up",
        Identifier = "sc-1",
        Author = "Rick Astley",
        SourceName = "soundcloud",
    };

    private static readonly TrackLoadResult QuebradoNoYouTube =
        TrackLoadResult.CreateError(new TrackException(ExceptionSeverity.Suspicious, "This video requires login", null));

    // Responde por modo de busca e anota a ordem em que eles foram pedidos.
    private static Func<TrackSearchMode, CancellationToken, ValueTask<TrackLoadResult>> Lavalink(
        List<TrackSearchMode> pedidos, TrackLoadResult youtube, TrackLoadResult soundcloud) =>
        (modo, _) =>
        {
            pedidos.Add(modo);
            return ValueTask.FromResult(modo == TrackSearchMode.SoundCloud ? soundcloud : youtube);
        };

    [Fact]
    public async Task Nome_que_o_YouTube_não_acha_vai_pro_SoundCloud()
    {
        List<TrackSearchMode> pedidos = [];

        var (resultado, erro) = await Music.BuscarAsync(
            Lavalink(pedidos, TrackLoadResult.CreateEmpty(), TrackLoadResult.CreateSearch([DoSoundCloud])),
            "never gonna give you up");

        Assert.Null(erro);
        Assert.Equal("soundcloud", resultado.Track!.SourceName);
        Assert.Equal([TrackSearchMode.YouTube, TrackSearchMode.SoundCloud], pedidos);
    }

    [Fact]
    public async Task Nome_que_o_YouTube_recusa_vai_pro_SoundCloud()
    {
        List<TrackSearchMode> pedidos = [];

        var (resultado, erro) = await Music.BuscarAsync(
            Lavalink(pedidos, QuebradoNoYouTube, TrackLoadResult.CreateSearch([DoSoundCloud])),
            "never gonna give you up");

        Assert.Null(erro);
        Assert.Equal("sc-1", resultado.Track!.Identifier);
    }

    [Fact]
    public async Task Sem_nada_no_SoundCloud_vale_o_erro_do_YouTube()
    {
        List<TrackSearchMode> pedidos = [];

        var (_, erro) = await Music.BuscarAsync(
            Lavalink(pedidos, QuebradoNoYouTube, TrackLoadResult.CreateEmpty()),
            "never gonna give you up");

        Assert.Contains("This video requires login", erro);
    }

    [Fact]
    public async Task Sem_nada_nos_dois_vale_o_que_o_YouTube_disse()
    {
        List<TrackSearchMode> pedidos = [];

        var (resultado, _) = await Music.BuscarAsync(
            Lavalink(pedidos, TrackLoadResult.CreateEmpty(), TrackLoadResult.CreateEmpty()),
            "never gonna give you up");

        Assert.False(resultado.HasMatches);
        Assert.Equal([TrackSearchMode.YouTube, TrackSearchMode.SoundCloud], pedidos);
    }

    [Fact]
    public async Task Se_o_YouTube_acha_o_SoundCloud_nem_é_chamado()
    {
        List<TrackSearchMode> pedidos = [];
        var doYouTube = DoSoundCloud with { Identifier = "yt-1", SourceName = "youtube" };

        var (resultado, _) = await Music.BuscarAsync(
            Lavalink(pedidos, TrackLoadResult.CreateSearch([doYouTube]), TrackLoadResult.CreateSearch([DoSoundCloud])),
            "never gonna give you up");

        Assert.Equal("yt-1", resultado.Track!.Identifier);
        Assert.Equal([TrackSearchMode.YouTube], pedidos);
    }

    [Fact]
    public async Task Link_não_tem_reserva()
    {
        List<TrackSearchMode> pedidos = [];

        var (_, erro) = await Music.BuscarAsync(
            Lavalink(pedidos, QuebradoNoYouTube, TrackLoadResult.CreateSearch([DoSoundCloud])),
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.NotNull(erro);
        Assert.Equal([TrackSearchMode.None], pedidos);
    }

    [Fact]
    public async Task Servidor_fora_na_reserva_não_esconde_o_que_o_YouTube_disse()
    {
        var (_, erro) = await Music.BuscarAsync(
            (modo, _) => modo == TrackSearchMode.SoundCloud
                ? throw new HttpRequestException("connection refused")
                : ValueTask.FromResult(QuebradoNoYouTube),
            "never gonna give you up");

        Assert.Contains("This video requires login", erro);
    }

    // ---- erros ---------------------------------------------------------------

    public static TheoryData<Exception> ErrosDoServidor =>
    [
        new HttpRequestException("connection refused"),
        new TimeoutException(),
        new TaskCanceledException(),
        new WebSocketException(),
        new InvalidOperationException("The node has no session identifier yet."),
        new Exception("por fora", new HttpRequestException("por dentro")),
    ];

    [Theory]
    [MemberData(nameof(ErrosDoServidor))]
    public void Lavalink_fora_é_reconhecido(Exception erro)
    {
        Assert.True(Music.ÉServidorFora(erro));
    }

    [Fact]
    public void Erro_meu_não_vira_desculpa_de_servidor()
    {
        Assert.False(Music.ÉServidorFora(new InvalidOperationException("outra coisa")));
        Assert.False(Music.ÉServidorFora(new ArgumentException()));
    }
}

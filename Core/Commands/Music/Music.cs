using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using AlephBot.Core.Personality;
using AlephBot.Threnodian.Youtube;

using Lavalink4NET;
using Lavalink4NET.Clients;

// sem este using o RetrieveAsync cai no da interface, que só sabe receber IDs crus
using Lavalink4NET.NetCord;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NetCord;
using NetCord.Rest;
using NetCord.Services;

namespace AlephBot.Core.Commands.Music;

// O que todo comando de música repete: pegar o player, traduzir a recusa e montar os embeds.
internal static class Music
{
    // O mesmo roxo do /help e do /mute — música não é notícia boa nem ruim.
    internal const int Roxo = 0x9B59B6;

    // Volume máximo aceito, em porcento. O Lavalink vai a 1000; ninguém precisa.
    internal const int VolumeMáximo = 200;

    // Quantas faixas o /queue lista antes de resumir o resto numa linha só.
    internal const int FilaVisível = 10;

    internal const string ExemploPosição = "1:30";

    private const int TamanhoBarra = 18;

    // O teto do Discord é 4096; a folga é pra linha de resumo do fim.
    private const int MaxDescrição = 3800;

    // O teto do Discord pro título do embed, onde agora vai o nome da faixa.
    private const int MaxTítulo = 256;

    // Quanto eu espero o servidor de áudio: ele sobe mais devagar que eu.
    private static readonly TimeSpan EsperaPeloServidor = TimeSpan.FromSeconds(10);

    private static readonly PlayerFactory<AlephPlayer, AlephPlayerOptions> Fábrica =
        PlayerFactory.Create<AlephPlayer, AlephPlayerOptions>(propriedades => new AlephPlayer(propriedades));

    // ---- player --------------------------------------------------------------

    // O player do servidor. conectar me põe no canal de quem chamou; exigirMesmoCanal, só quem age.
    internal static ValueTask<PlayerResult<AlephPlayer>> ObterAsync(
        IAudioService áudio,
        IGuildContext contexto,
        TextChannel? canal,
        bool conectar,
        bool exigirMesmoCanal = true,
        CancellationToken cancellationToken = default)
    {
        var opções = new PlayerRetrieveOptions(
            ChannelBehavior: conectar ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None,
            VoiceStateBehavior: exigirMesmoCanal
                ? MemberVoiceStateBehavior.RequireSame
                : MemberVoiceStateBehavior.Ignore);

        return áudio.Players.RetrieveAsync<AlephPlayer, AlephPlayerOptions>(
            contexto,
            playerFactory: Fábrica,
            configure: player => Configurar(player, canal),
            retrieveOptions: opções,
            cancellationToken: cancellationToken);
    }

    // O mesmo player sem ninguém pedir: é como eu volto pro canal depois de um restart.
    internal static ValueTask<PlayerResult<AlephPlayer>> VoltarAsync(
        IAudioService áudio,
        ulong guildId,
        ulong canalDeVoz,
        TextChannel? canal,
        CancellationToken cancellationToken = default)
    {
        var opções = new PlayerRetrieveOptions(
            ChannelBehavior: PlayerChannelBehavior.Join,
            VoiceStateBehavior: MemberVoiceStateBehavior.Ignore);

        return áudio.Players.RetrieveAsync(
            guildId,
            canalDeVoz,
            playerFactory: Fábrica,
            options: Options.Create(Configurar(new AlephPlayerOptions(), canal)),
            retrieveOptions: opções,
            cancellationToken: cancellationToken);
    }

    private static AlephPlayerOptions Configurar(AlephPlayerOptions player, TextChannel? canal)
    {
        player.Canal = canal;

        // surdo desde o começo: eu não escuto ninguém, e o Discord mostra isso
        player.SelfDeaf = true;

        // sem isto o /stop me tira do canal junto, e não sobra o que o /resume reaproveite
        player.DisconnectOnStop = false;

        return player;
    }

    // O player pronto, ou a frase que explica o porquê — recusa e servidor fora dão no mesmo.
    internal static async Task<(AlephPlayer? Player, string? Erro)> PlayerAsync(
        IAudioService áudio,
        IGuildContext contexto,
        TextChannel? canal,
        bool conectar,
        bool exigirMesmoCanal = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await EsperarServidorAsync(áudio, cancellationToken) is { } fora)
                return (null, fora);

            var resultado = await ObterAsync(
                áudio, contexto, canal, conectar, exigirMesmoCanal, cancellationToken);

            return (resultado.Player, Recusa(resultado.Status));
        }
        catch (TimeoutException) when (conectar)
        {
            // o Discord não confirmou a entrada, e quase sempre é permissão de Conectar faltando
            return (null, Denia.MúsicaNãoConsegui());
        }
        catch (Exception ex) when (ÉServidorFora(ex))
        {
            return (null, Denia.MúsicaServidorFora());
        }
    }

    // Null quando o servidor de áudio está de pé, senão a frase de que ele não está. Sem esperar,
    // comando dado após o boot morria com um erro de sessão interno; de pé, isto volta na hora.
    private static async Task<string?> EsperarServidorAsync(
        IAudioService áudio, CancellationToken cancellationToken)
    {
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        espera.CancelAfter(EsperaPeloServidor);

        try
        {
            await áudio.WaitForReadyAsync(espera.Token);

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // esperei o que dava; a essa altura é o servidor que não está lá
            return Denia.MúsicaServidorFora();
        }
    }

    // A frase que explica por que não deu, ou null quando o player veio.
    internal static string? Recusa(PlayerRetrieveStatus status) => status switch
    {
        PlayerRetrieveStatus.Success => null,
        PlayerRetrieveStatus.UserNotInVoiceChannel => Denia.MúsicaVocêForaDoCanal(),
        PlayerRetrieveStatus.VoiceChannelMismatch => Denia.MúsicaCanalDiferente(),
        PlayerRetrieveStatus.BotNotConnected => Denia.MúsicaNãoEstouTocando(),
        _ => Denia.MúsicaNãoConsegui(),
    };

    // Separa "o Lavalink não está lá" de um erro meu; o resto sobe pro log.
    internal static bool ÉServidorFora(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException or WebSocketException
        // nó ainda conectando: o Lavalink4NET avisa por texto, não tem exceção própria pra isso
        || (ex is InvalidOperationException && ex.Message.Contains("session identifier", StringComparison.OrdinalIgnoreCase))
        || (ex.InnerException is { } dentro && ÉServidorFora(dentro));

    // O que um comando devolve. Depois roda já com a resposta no canal, senão o anúncio sai antes.
    internal readonly record struct Resposta(
        EmbedProperties? Embed,
        string? Erro,
        Func<Task>? Depois = null,
        Cartão? Cartão = null)
    {
        internal static Resposta Ok(EmbedProperties embed, Func<Task>? depois = null) => new(embed, null, depois);

        // A resposta que vai no formato novo, com a capa grande dentro do card.
        internal static Resposta Ok(Cartão cartão) => new(null, null, null, cartão);

        internal static Resposta Falha(string erro) => new(null, erro);
    }

    // ---- busca ---------------------------------------------------------------

    // Link vai cru pro Lavalink, texto vira busca. Tira a rádio (RD...) que o YouTube inventa.
    internal static string SemRádio(string busca)
    {
        if (!Uri.TryCreate(busca, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return busca;
        }

        var lista = Parâmetro(uri.Query, "list");

        // "RD" é o prefixo das mixes; start_radio=1 aparece quando o link nasce do autoplay
        var éRádio = lista?.StartsWith("RD", StringComparison.Ordinal) is true
            || Parâmetro(uri.Query, "start_radio") is "1";

        if (!éRádio)
            return busca;

        // no link normal o vídeo é o ?v=; no encurtado (youtu.be/ID) ele é o caminho
        var vídeo = Parâmetro(uri.Query, "v")
            ?? (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase)
                ? uri.AbsolutePath.Trim('/')
                : null);

        // rádio sem vídeo identificável não tem o que salvar; deixo passar como veio
        return vídeo is { Length: > 0 }
            ? $"https://www.youtube.com/watch?v={vídeo}"
            : busca;
    }

    private static string? Parâmetro(string query, string nome)
    {
        foreach (var parte in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var igual = parte.IndexOf('=');

            if (igual > 0 && parte.AsSpan(0, igual).SequenceEqual(nome))
                return Uri.UnescapeDataString(parte[(igual + 1)..]);
        }

        return null;
    }

    internal static TrackSearchMode ModoDeBusca(string busca) =>
        ÉLink(busca) ? TrackSearchMode.None : TrackSearchMode.YouTube;

    internal static bool ÉLink(string texto) =>
        Uri.TryCreate(texto, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // Pede ao Lavalink e traduz o erro. Pode vir vazio, e o que dizer aí fica com o chamador.
    internal static async Task<(TrackLoadResult Resultado, string? Erro)> CarregarAsync(
        IAudioService áudio, string busca, CancellationToken cancellationToken = default)
    {
        // a busca anda junto com a entrada no canal, então ela espera o servidor por conta própria
        if (await EsperarServidorAsync(áudio, cancellationToken) is { } fora)
            return (default, fora);

        TrackLoadResult resultado;

        try
        {
            resultado = await áudio.Tracks.LoadTracksAsync(
                busca, ModoDeBusca(busca), cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ÉServidorFora(ex))
        {
            return (default, Denia.MúsicaServidorFora());
        }

        if (resultado.IsFailed)
        {
            return (default, Denia.MúsicaBuscaFalhou(
                resultado.Exception?.Message ?? "o servidor não disse por quê"));
        }

        return (resultado, null);
    }

    // A espera que começou antes de alguém precisar dela. Se o comando desistir no caminho — o
    // canal de voz recusou, por exemplo —, a falha dela morre aqui, e não como tarefa solta.
    internal static Task<T> Adiantada<T>(Task<T> tarefa)
    {
        _ = tarefa.ContinueWith(static feita => _ = feita.Exception, TaskContinuationOptions.OnlyOnFaulted);

        return tarefa;
    }

    // ---- embeds --------------------------------------------------------------

    // O anúncio da faixa que começou, com a próxima numa linha embaixo quando tem fila.
    internal static Task<Cartão> CartãoTocandoAsync(
        LavalinkTrack faixa,
        string? quemPediu,
        LavalinkTrack? próxima = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default) =>
        CartãoDaFaixa.TocandoAsync(faixa, quemPediu, próxima, logger, cancellationToken);

    // A primeira da fila, ou null quando a faixa de agora é a última.
    internal static LavalinkTrack? Próxima(AlephPlayer player) =>
        player.Queue.Count > 0 ? player.Queue[0].Track : null;

    internal static EmbedProperties EmbedNaFila(LavalinkTrack faixa, int posição, string? quemPediu) =>
        Base(Denia.MúsicaTítuloNaFila, faixa, quemPediu, Denia.MúsicaEntrouNaFila())
            .ComCampo(Denia.MúsicaCampoPosição, $"#{posição}", inline: true);

    // A playlist inteira num embed. começouAgora troca o rótulo: "Agora" em faixa parada mente.
    internal static EmbedProperties EmbedPlaylist(
        string nome, IReadOnlyList<LavalinkTrack> faixas, string? quemPediu, bool começouAgora)
    {
        var descrição = new StringBuilder(Denia.MúsicaPlaylistEntrou(faixas.Count));

        if (faixas.Count > 0)
        {
            var rótulo = começouAgora ? Denia.MúsicaCampoAgora : Denia.MúsicaPlaylistComeçaPor;
            descrição.Append('\n').Append(rótulo).Append(": ").Append(Link(faixas[0]));
        }

        // sem capa e sem duração: o "tocando agora" logo abaixo já traz isso
        var embed = new EmbedProperties
        {
            Author = new EmbedAuthorProperties { Name = Denia.MúsicaTítuloPlaylist },
            Title = Título(nome),
            Description = descrição.ToString(),
            Color = new Color(Roxo),
        };

        return embed.ComRodapé(quemPediu, faixas.Count > 0 ? faixas[0].SourceName : null);
    }

    // O aviso de que voltei depois de um restart, no lugar do "tocando agora".
    internal static EmbedProperties EmbedVoltei(LavalinkTrack faixa, TimeSpan? posição, bool pausada, int naFila)
    {
        var texto = new StringBuilder(
            posição is { } de
                ? Denia.MúsicaVoltei(Duração(de))
                : Denia.MúsicaVolteiDoComeço());

        if (pausada)
            texto.Append(' ').Append(Denia.MúsicaVolteiPausada());

        if (naFila > 0)
            texto.Append(' ').Append(Denia.MúsicaFilaVeioJunto(naFila));

        var embed = new EmbedProperties
        {
            Author = new EmbedAuthorProperties { Name = Denia.MúsicaTítuloVoltei },
            Title = Título(faixa.Title),
            Url = faixa.Uri?.ToString(),
            Description = texto.ToString(),
            Color = new Color(Roxo),
        };

        if (faixa.ArtworkUri is { } capa)
            embed.Image = new EmbedImageProperties(capa.ToString());

        return embed.ComRodapé(null, faixa.SourceName);
    }

    // O /nowplaying: a mesma faixa do anúncio, com a barra de progresso viva.
    internal static Task<Cartão> CartãoProgressoAsync(AlephPlayer player, LavalinkTrack faixa)
    {
        var posição = player.Position?.Position ?? TimeSpan.Zero;

        var progresso = faixa.IsLiveStream
            ? Denia.MúsicaAoVivo
            : $"{Barra(posição, faixa.Duration)}\n`{Duração(posição)} / {Duração(faixa.Duration)}`";

        return CartãoDaFaixa.ProgressoAsync(player, faixa, progresso);
    }

    internal static EmbedProperties EmbedFila(AlephPlayer player)
    {
        var fila = player.Queue;

        var linhas = new StringBuilder();

        if (player.CurrentTrack is { } atual)
            linhas.Append($"**{Denia.MúsicaCampoAgora}** · {Link(atual)}\n\n");

        var mostradas = 0;

        for (var i = 0; i < Math.Min(fila.Count, FilaVisível); i++)
        {
            var faixa = fila[i].Track;

            var linha = faixa is null
                ? $"`{i + 1}.` —\n"
                : $"`{i + 1}.` {Link(faixa)} · `{DuraçãoDe(faixa)}`\n";

            // título quilométrico estoura o teto do embed, e embed recusado não mostra fila nenhuma
            if (linhas.Length + linha.Length > MaxDescrição)
                break;

            linhas.Append(linha);
            mostradas++;
        }

        if (fila.Count > mostradas)
            linhas.Append($"\n{Denia.MúsicaFilaSobra(fila.Count - mostradas)}");

        return new EmbedProperties
        {
            Title = Denia.MúsicaTítuloFila,
            Description = linhas.ToString(),
            Color = new Color(Roxo),
            Fields =
            [
                new EmbedFieldProperties
                {
                    Name = Denia.MúsicaCampoTotal,
                    Value = Denia.MúsicaFilaResumo(fila.Count, Duração(Soma([.. fila.Select(f => f.Track).OfType<LavalinkTrack>()]))),
                    Inline = true,
                },
                new EmbedFieldProperties
                {
                    Name = Denia.MúsicaCampoRepetição,
                    Value = Repetição(player.RepeatMode),
                    Inline = true,
                },
            ],
            Footer = new EmbedFooterProperties { Text = Denia.Assinatura },
        };
    }

    // Aviso simples de uma linha — o que quase todo comando devolve.
    internal static EmbedProperties Aviso(string texto) =>
        new()
        {
            Description = texto,
            Color = new Color(Roxo),
        };

    // O esqueleto dos embeds de faixa: rótulo pequeno em cima, nome da faixa no título, clicável.
    private static EmbedProperties Base(
        string rótulo, LavalinkTrack faixa, string? quemPediu, string descrição)
    {
        var embed = new EmbedProperties
        {
            Author = new EmbedAuthorProperties { Name = rótulo },
            Title = Título(faixa.Title),
            Url = faixa.Uri?.ToString(),
            Description = descrição,
            Color = new Color(Roxo),
            Fields =
            [
                new EmbedFieldProperties { Name = Denia.MúsicaCampoArtista, Value = Artista(faixa), Inline = true },
                new EmbedFieldProperties { Name = Denia.MúsicaCampoDuração, Value = $"`{DuraçãoDe(faixa)}`", Inline = true },
            ],
        };

        if (faixa.ArtworkUri is { } capa)
            embed.Image = new EmbedImageProperties(capa.ToString());

        return embed.ComRodapé(quemPediu, faixa.SourceName);
    }

    private static EmbedProperties ComCampo(this EmbedProperties embed, string nome, string valor, bool inline)
    {
        embed.Fields = [.. embed.Fields ?? [], new EmbedFieldProperties { Name = nome, Value = valor, Inline = inline }];
        return embed;
    }

    private static EmbedProperties ComRodapé(this EmbedProperties embed, string? quemPediu, string? fonte)
    {
        embed.Footer = new EmbedFooterProperties { Text = Denia.MúsicaRodapé(quemPediu, fonte) };
        return embed;
    }

    // O título do embed não lê markdown — vai cru, só cortado no teto do Discord.
    private static string Título(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? "?"
            : texto.Length <= MaxTítulo ? texto
            : $"{texto[..(MaxTítulo - 1)]}…";

    // ---- formatação ----------------------------------------------------------

    // Quem pediu a faixa, ou null quando ela entrou por caminho que não guarda isso.
    internal static string? QuemPediu(ITrackQueueItem? item) =>
        (item as FaixaPedida)?.QuemPediu;

    // Título clicável. O texto é escapado porque nome de faixa vem cheio de `[`, `]` e `*`.
    internal static string Link(LavalinkTrack faixa) =>
        faixa.Uri is { } uri
            ? $"[{Escapa(faixa.Title)}]({uri})"
            : $"**{Escapa(faixa.Title)}**";

    // O artista, clicável quando eu sei o canal dele — pro YouTube quem conta é o yt-audio.
    internal static string Artista(LavalinkTrack faixa)
    {
        var nome = Escapa(faixa.Author);

        return CanalDoArtista(faixa) is { } canal ? $"[{nome}]({canal})" : nome;
    }

    private static string? CanalDoArtista(LavalinkTrack faixa)
    {
        if (faixa is FaixaDoYoutube { Canal: { Length: > 0 } doYoutube })
            return doYoutube;

        // o LavaSrc (Spotify, Deezer, Apple) manda o artista no pluginInfo da faixa
        if (faixa.AdditionalInformation.TryGetValue("artistUrl", out var valor)
            && valor.ValueKind is JsonValueKind.String
            && valor.GetString() is { Length: > 0 } endereço)
        {
            return endereço;
        }

        return null;
    }

    // O Escapa de quem não tem título: faixa sem nome vira "?".
    internal static string Escapa(string? texto) =>
        string.IsNullOrEmpty(texto) ? "?" : Markdown.Escapa(texto);

    internal static string DuraçãoDe(LavalinkTrack faixa) =>
        faixa.IsLiveStream ? Denia.MúsicaAoVivo : Duração(faixa.Duration);

    // "3:41" quando cabe na hora, "1:02:03" quando não cabe.
    internal static string Duração(TimeSpan tempo) =>
        tempo.TotalHours >= 1
            ? $"{(int)tempo.TotalHours}:{tempo.Minutes:D2}:{tempo.Seconds:D2}"
            : $"{tempo.Minutes}:{tempo.Seconds:D2}";

    internal static int Porcento(float volume) => (int)Math.Round(volume * 100);

    internal static string Repetição(TrackRepeatMode modo) => modo switch
    {
        TrackRepeatMode.Track => "🔂",
        TrackRepeatMode.Queue => "🔁",
        _ => "➖",
    };

    // Soma que ignora transmissão ao vivo — somar "infinito" não informa nada.
    private static TimeSpan Soma(IReadOnlyList<LavalinkTrack> faixas)
    {
        var total = TimeSpan.Zero;

        foreach (var faixa in faixas)
        {
            if (!faixa.IsLiveStream)
                total += faixa.Duration;
        }

        return total;
    }

    private static string Barra(TimeSpan posição, TimeSpan duração)
    {
        if (duração <= TimeSpan.Zero)
            return new string('▬', TamanhoBarra);

        var andou = Math.Clamp((int)(posição / duração * TamanhoBarra), 0, TamanhoBarra - 1);

        return $"{new string('▬', andou)}🔘{new string('▬', TamanhoBarra - 1 - andou)}";
    }

    // ---- entrada do usuário --------------------------------------------------

    // Lê "1:30", "1:02:03", "90" e "1m30s"; null quando o texto não vira posição.
    internal static TimeSpan? ParsePosição(string texto)
    {
        texto = texto.Trim();

        if (texto.Length == 0)
            return null;

        return texto.Contains(':')
            ? PorRelógio(texto)
            : PorUnidade(texto);
    }

    // "mm:ss" e "hh:mm:ss" — o formato que aparece no player do YouTube.
    private static TimeSpan? PorRelógio(string texto)
    {
        var partes = texto.Split(':');

        if (partes.Length is < 2 or > 3)
            return null;

        var total = TimeSpan.Zero;

        foreach (var parte in partes)
        {
            if (!int.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out var valor))
                return null;

            // NumberStyles.None já recusa sinal e espaço; sobra o campo vazio de "1::30"
            if (parte.Length == 0)
                return null;

            total = total * 60 + TimeSpan.FromSeconds(valor);
        }

        return total;
    }

    // "90", "1m30s", "2m" — número solto fecha em segundos.
    private static TimeSpan? PorUnidade(string texto)
    {
        var total = TimeSpan.Zero;
        long número = 0;
        var temNúmero = false;

        foreach (var c in texto)
        {
            if (char.IsAsciiDigit(c))
            {
                número = número * 10 + (c - '0');
                temNúmero = true;

                // faixa nenhuma tem 10⁷ segundos; corto aqui pra não estourar o TimeSpan
                if (número > 9_999_999)
                    return null;

                continue;
            }

            if (!temNúmero)
                return null;

            var unidade = char.ToLowerInvariant(c) switch
            {
                's' => TimeSpan.FromSeconds(1),
                'm' => TimeSpan.FromMinutes(1),
                'h' => TimeSpan.FromHours(1),
                _ => TimeSpan.Zero,
            };

            if (unidade == TimeSpan.Zero)
                return null;

            total += unidade * número;
            número = 0;
            temNúmero = false;
        }

        if (temNúmero)
            total += TimeSpan.FromSeconds(número);

        return total;
    }
}

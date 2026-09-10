using System.Globalization;
using System.Net.WebSockets;
using System.Text;

using AlephBot.Core.Personality;

using Lavalink4NET;
using Lavalink4NET.Clients;

// sem este using o RetrieveAsync cai no da interface, que só sabe receber IDs crus
using Lavalink4NET.NetCord;

using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

using NetCord;
using NetCord.Rest;
using NetCord.Services;

namespace AlephBot.Core.Commands.Music;

/// <summary>
/// O que todo comando de música repete: pegar o player do servidor, traduzir a recusa do
/// Lavalink pra uma frase da Denia e montar os embeds. Cada comando cuida só da própria ação.
/// </summary>
internal static class Music
{
    /// <summary>O mesmo roxo do /help e do /mute — música não é notícia boa nem ruim.</summary>
    internal const int Roxo = 0x9B59B6;

    /// <summary>Volume máximo aceito, em porcento. O Lavalink vai a 1000; ninguém precisa.</summary>
    internal const int VolumeMáximo = 200;

    /// <summary>Quantas faixas o /queue lista antes de resumir o resto numa linha só.</summary>
    internal const int FilaVisível = 10;

    internal const string ExemploPosição = "1:30";

    private const int TamanhoBarra = 18;

    /// <summary>O teto do Discord é 4096; a folga é pra linha de resumo do fim.</summary>
    private const int MaxDescrição = 3800;

    private static readonly PlayerFactory<AlephPlayer, AlephPlayerOptions> Fábrica =
        PlayerFactory.Create<AlephPlayer, AlephPlayerOptions>(propriedades => new AlephPlayer(propriedades));

    // ---- player --------------------------------------------------------------

    /// <summary>
    /// O player deste servidor. Com <paramref name="conectar"/> eu entro no canal de quem
    /// chamou; sem ele, quem não tem player leva
    /// <see cref="PlayerRetrieveStatus.BotNotConnected"/> em vez de me arrastar pra voz.
    ///
    /// <paramref name="exigirMesmoCanal"/> impede alguém de outro canal pausar a música dos
    /// outros. Fica falso pra quem só olha: exigir voz pra ler a fila seria implicância.
    /// </summary>
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
            configure: player =>
            {
                player.Canal = canal;

                // surdo desde o começo: eu não escuto ninguém, e o Discord mostra isso
                player.SelfDeaf = true;

                // sem isto o /stop me tira do canal junto, e aí não sobra o que /resume
                // ou /play possa reaproveitar — quem me tira do canal é o /disconnect
                player.DisconnectOnStop = false;
            },
            retrieveOptions: opções,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// O player pronto, ou a frase que explica por que não deu. Recusa e servidor fora do ar
    /// saem pelo mesmo caminho: pra quem digitou, os dois são "não rolou".
    /// </summary>
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
            var resultado = await ObterAsync(
                áudio, contexto, canal, conectar, exigirMesmoCanal, cancellationToken);

            return (resultado.Player, Recusa(resultado.Status));
        }
        catch (TimeoutException) when (conectar)
        {
            // o Discord não confirmou a entrada, e quase sempre é permissão de Conectar
            // faltando. culpar o Lavalink aqui manda o cara procurar no lugar errado
            return (null, Denia.MúsicaNãoConsegui());
        }
        catch (Exception ex) when (ÉServidorFora(ex))
        {
            return (null, Denia.MúsicaServidorFora());
        }
    }

    /// <summary>A frase que explica por que não deu, ou null quando o player veio.</summary>
    internal static string? Recusa(PlayerRetrieveStatus status) => status switch
    {
        PlayerRetrieveStatus.Success => null,
        PlayerRetrieveStatus.UserNotInVoiceChannel => Denia.MúsicaVocêForaDoCanal(),
        PlayerRetrieveStatus.VoiceChannelMismatch => Denia.MúsicaCanalDiferente(),
        PlayerRetrieveStatus.BotNotConnected => Denia.MúsicaNãoEstouTocando(),
        _ => Denia.MúsicaNãoConsegui(),
    };

    /// <summary>
    /// Separa "o Lavalink não está lá" de um erro meu. Conexão recusada, senha errada e
    /// timeout viram a frase do servidor; o resto sobe pro log, em vez de virar desculpa.
    /// </summary>
    internal static bool ÉServidorFora(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException or WebSocketException
        || (ex.InnerException is { } dentro && ÉServidorFora(dentro));

    /// <summary>O que um comando de música devolve: o embed pronto, ou a recusa em texto.</summary>
    internal readonly record struct Resposta(EmbedProperties? Embed, string? Erro)
    {
        internal static Resposta Ok(EmbedProperties embed) => new(embed, null);

        internal static Resposta Falha(string erro) => new(null, erro);
    }

    // ---- busca ---------------------------------------------------------------

    /// <summary>
    /// Link vai cru pro Lavalink, que resolve a fonte sozinho; texto solto vira busca no
    /// YouTube, que é o que quem digita o nome da música espera.
    /// </summary>
    /// <summary>
    /// Tira a rádio automática de um link de vídeo.
    ///
    /// Quem copia o link direto do player do YouTube leva junto <c>&amp;list=RD...</c> — a
    /// rádio que ele monta sozinho a partir da faixa. O Lavalink obedece e carrega as cem
    /// faixas dela, então um "toca essa música" vira uma fila que ninguém pediu.
    ///
    /// Só a rádio sai. Playlist de verdade (<c>list=PL...</c>) continua entrando inteira:
    /// aquela a pessoa escolheu, esta o YouTube inventou.
    /// </summary>
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
        Uri.TryCreate(busca, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? TrackSearchMode.None
            : TrackSearchMode.YouTube;

    // ---- embeds --------------------------------------------------------------

    internal static EmbedProperties EmbedTocando(LavalinkTrack faixa, string? quemPediu, float volume) =>
        Base(Denia.MúsicaTítuloTocando, faixa, quemPediu, Denia.MúsicaComeçou(Escapa(faixa.Title)))
            .ComCampo(Denia.MúsicaCampoVolume, $"{Porcento(volume)}%", inline: true);

    internal static EmbedProperties EmbedNaFila(LavalinkTrack faixa, int posição, string? quemPediu) =>
        Base(
            Denia.MúsicaTítuloNaFila,
            faixa,
            quemPediu,
            Denia.MúsicaEntrouNaFila(Escapa(faixa.Title), posição))
            .ComCampo(Denia.MúsicaCampoPosição, $"#{posição}", inline: true);

    internal static EmbedProperties EmbedPlaylist(
        string nome, IReadOnlyList<LavalinkTrack> faixas, string? quemPediu)
    {
        var embed = new EmbedProperties
        {
            Title = Denia.MúsicaTítuloPlaylist,
            Description = Denia.MúsicaPlaylistEntrou(Escapa(nome), faixas.Count),
            Color = new Color(Roxo),
            Fields =
            [
                new EmbedFieldProperties
                {
                    Name = Denia.MúsicaCampoDuração,
                    Value = $"`{Duração(Soma(faixas))}`",
                    Inline = true,
                },
            ],
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (faixas.Count > 0 && faixas[0].ArtworkUri is { } capa)
            embed.Thumbnail = new EmbedThumbnailProperties(capa.ToString());

        return quemPediu is null ? embed : embed.ComRodapé(quemPediu);
    }

    /// <summary>O /nowplaying: a mesma faixa do anúncio, com a barra de progresso viva.</summary>
    internal static EmbedProperties EmbedProgresso(AlephPlayer player, LavalinkTrack faixa)
    {
        var posição = player.Position?.Position ?? TimeSpan.Zero;

        var progresso = faixa.IsLiveStream
            ? Denia.MúsicaAoVivo
            : $"{Barra(posição, faixa.Duration)}\n`{Duração(posição)} / {Duração(faixa.Duration)}`";

        var embed = new EmbedProperties
        {
            Title = player.IsPaused ? $"⏸️ {Denia.MúsicaTítuloTocando}" : Denia.MúsicaTítuloTocando,
            Description = $"{Link(faixa)}\n{progresso}",
            Color = new Color(Roxo),
            Fields =
            [
                new EmbedFieldProperties { Name = Denia.MúsicaCampoArtista, Value = Escapa(faixa.Author), Inline = true },
                new EmbedFieldProperties { Name = Denia.MúsicaCampoVolume, Value = $"{Porcento(player.Volume)}%", Inline = true },
                new EmbedFieldProperties { Name = Denia.MúsicaCampoRepetição, Value = Repetição(player.RepeatMode), Inline = true },
            ],
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (faixa.ArtworkUri is { } capa)
            embed.Thumbnail = new EmbedThumbnailProperties(capa.ToString());

        return QuemPediu(player.CurrentItem) is { } quem ? embed.ComRodapé(quem) : embed;
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

            // dez títulos quilométricos estouram o teto do embed, e embed recusado não
            // mostra fila nenhuma. melhor listar menos e avisar quantas ficaram de fora
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
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Aviso simples de uma linha — o que quase todo comando devolve.</summary>
    internal static EmbedProperties Aviso(string texto) =>
        new()
        {
            Description = texto,
            Color = new Color(Roxo),
        };

    private static EmbedProperties Base(
        string título, LavalinkTrack faixa, string? quemPediu, string descrição)
    {
        var embed = new EmbedProperties
        {
            Title = título,
            Description = $"{descrição}\n{Link(faixa)}",
            Color = new Color(Roxo),
            Fields =
            [
                new EmbedFieldProperties { Name = Denia.MúsicaCampoArtista, Value = Escapa(faixa.Author), Inline = true },
                new EmbedFieldProperties { Name = Denia.MúsicaCampoDuração, Value = $"`{DuraçãoDe(faixa)}`", Inline = true },
            ],
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (faixa.ArtworkUri is { } capa)
            embed.Thumbnail = new EmbedThumbnailProperties(capa.ToString());

        if (!string.IsNullOrWhiteSpace(faixa.SourceName))
            embed.Fields = [.. embed.Fields!, new EmbedFieldProperties { Name = Denia.MúsicaCampoFonte, Value = faixa.SourceName, Inline = true }];

        return quemPediu is null ? embed : embed.ComRodapé(quemPediu);
    }

    private static EmbedProperties ComCampo(this EmbedProperties embed, string nome, string valor, bool inline)
    {
        embed.Fields = [.. embed.Fields ?? [], new EmbedFieldProperties { Name = nome, Value = valor, Inline = inline }];
        return embed;
    }

    private static EmbedProperties ComRodapé(this EmbedProperties embed, string quemPediu)
    {
        embed.Footer = new EmbedFooterProperties { Text = Denia.MúsicaRodapé(quemPediu) };
        return embed;
    }

    // ---- formatação ----------------------------------------------------------

    /// <summary>Quem pediu a faixa, ou null quando ela entrou por caminho que não guarda isso.</summary>
    internal static string? QuemPediu(ITrackQueueItem? item) =>
        (item as FaixaPedida)?.QuemPediu;

    /// <summary>
    /// Título clicável. O texto do link é escapado porque nome de faixa vem com `[`, `]` e
    /// `*` o tempo todo, e um colchete solto quebra a sintaxe do markdown do Discord.
    /// </summary>
    internal static string Link(LavalinkTrack faixa) =>
        faixa.Uri is { } uri
            ? $"[{Escapa(faixa.Title)}]({uri})"
            : $"**{Escapa(faixa.Title)}**";

    internal static string Escapa(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return "?";

        var saída = new StringBuilder(texto.Length + 8);

        foreach (var c in texto)
        {
            if (c is '[' or ']' or '(' or ')' or '*' or '_' or '~' or '`' or '\\' or '|' or '>')
                saída.Append('\\');

            saída.Append(c);
        }

        return saída.ToString();
    }

    internal static string DuraçãoDe(LavalinkTrack faixa) =>
        faixa.IsLiveStream ? Denia.MúsicaAoVivo : Duração(faixa.Duration);

    /// <summary>"3:41" quando cabe na hora, "1:02:03" quando não cabe.</summary>
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

    /// <summary>Soma que ignora transmissão ao vivo — somar "infinito" não informa nada.</summary>
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

    /// <summary>
    /// Lê "1:30", "1:02:03", "90" (segundos) e "1m30s". Devolve null quando o texto não vira
    /// uma posição — negativo incluso, que só apareceria por engano de digitação.
    /// </summary>
    internal static TimeSpan? ParsePosição(string texto)
    {
        texto = texto.Trim();

        if (texto.Length == 0)
            return null;

        return texto.Contains(':')
            ? PorRelógio(texto)
            : PorUnidade(texto);
    }

    /// <summary>"mm:ss" e "hh:mm:ss" — o formato que aparece no player do YouTube.</summary>
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

    /// <summary>"90", "1m30s", "2m" — número solto fecha em segundos.</summary>
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

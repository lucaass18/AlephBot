using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Interface;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Anime;

/// <summary>
/// /ma anime, /ma manga — procura no MyAnimeList e mostra a ficha: capa, nota, ranking,
/// formato, gêneros e a sinopse inteira, traduzida pro português.
/// </summary>
[SlashCommand("ma", "Procura um anime ou mangá no MyAnimeList.")]
public sealed class MyAnimelistCommand : AlephSlashModule
{
    private readonly IMyAnimeList _mal;
    private readonly GoogleTranslator _tradutor;

    public MyAnimelistCommand(IMyAnimeList mal, GoogleTranslator tradutor)
    {
        _mal = mal;
        _tradutor = tradutor;
    }

    public override string Name => "ma";
    public override string Description => "Procura um anime ou mangá no MyAnimeList.";
    public override CommandCategory Category => CommandCategory.Anime;
    public override string? Usage => MyAnimelist.Uso;

    [SubSlashCommand("anime", "Procura um anime no MyAnimeList.")]
    public Task AnimeAsync(
        [SlashCommandParameter(Name = "nome", Description = "O nome do anime.")] string nome) =>
        ExecutarAsync(TipoDeObra.Anime, nome);

    [SubSlashCommand("manga", "Procura um mangá no MyAnimeList.")]
    public Task MangaAsync(
        [SlashCommandParameter(Name = "nome", Description = "O nome do mangá.")] string nome) =>
        ExecutarAsync(TipoDeObra.Manga, nome);

    private async Task ExecutarAsync(TipoDeObra tipo, string nome)
    {
        // perguntar ao MAL e depois ao tradutor passa fácil dos 3 segundos que o Discord dá; peço tempo
        await DeferAsync();

        var (embed, erro) = await MyAnimelist.ProcurarAsync(_mal, _tradutor, tipo, nome, Context.User.Username);

        if (erro is not null)
            await EditarComErroAsync(erro);
        else
            await EditarRespostaAsync(embed!);
    }
}

/// <summary>!ma anime/mangá — a mesma ficha do /ma, para quem prefere o prefixo.</summary>
public sealed class MyAnimelistTextCommand : AlephTextModule
{
    private readonly IMyAnimeList _mal;
    private readonly GoogleTranslator _tradutor;

    public MyAnimelistTextCommand(IMyAnimeList mal, GoogleTranslator tradutor)
    {
        _mal = mal;
        _tradutor = tradutor;
    }

    public override string Name => "ma";
    public override string Description => "Procura um anime ou mangá no MyAnimeList.";
    public override CommandCategory Category => CommandCategory.Anime;
    public override string? Usage => MyAnimelist.Uso;

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [Command("ma", "mal", "myanimelist")]
    public async Task MaAsync(string tipo, [CommandParameter(Remainder = true)] string nome)
    {
        // o comando de texto não tem subcomando de verdade: a primeira palavra faz o papel
        if (MyAnimelist.LerTipo(tipo) is not { } tipoDeObra)
        {
            await ErrorAsync(Denia.MalTipoDesconhecido(tipo, MyAnimelist.Uso));
            return;
        }

        // a resposta demora uns segundos; o "digitando..." é o jeito de dizer que eu ouvi
        await Context.Client.Rest.TriggerTypingAsync(Context.Message.ChannelId);

        var (embed, erro) = await MyAnimelist.ProcurarAsync(_mal, _tradutor, tipoDeObra, nome, Context.User.Username);

        if (erro is not null)
            await ErrorAsync(erro);
        else
            await ReplyAsync(embed!);
    }
}

internal static class MyAnimelist
{
    internal const string Uso = "ma <anime|mangá> <nome>";

    /// <summary>O MAL não procura por menos que isso: devolve 400 "invalid q".</summary>
    internal const int BuscaMínima = 3;

    /// <summary>O azul do MyAnimeList.</summary>
    private const int Azul = 0x2E51A2;

    // tetos do Discord: 256 no título, 4096 na descrição, 1024 num campo, 6000 no embed inteiro.
    // a sinopse para bem antes do teto dela pra sobrar espaço pro resto
    private const int MaxTítulo = 256;
    private const int MaxSinopse = 3000;
    private const int MaxCampo = 1024;

    // o MAL assina as sinopses que reescreveu; a assinatura não é parte da história
    private static readonly Regex AssinaturaDoMal = new(
        @"\s*\[Written by MAL Rewrite\]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>"anime", "mangá", "manga", "Animes"... o que a pessoa digitou no lugar do subcomando.</summary>
    internal static TipoDeObra? LerTipo(string texto)
    {
        var limpo = SemAcentos(texto.Trim()).ToLowerInvariant();

        if (limpo.StartsWith("anime", StringComparison.Ordinal))
            return TipoDeObra.Anime;

        if (limpo.StartsWith("manga", StringComparison.Ordinal))
            return TipoDeObra.Manga;

        return null;
    }

    /// <summary>
    /// Procura, escolhe o resultado, traduz a sinopse e monta a ficha. O /ma e o !ma caem
    /// os dois aqui; o que volta é o embed pronto ou a frase que explica por que não deu.
    /// </summary>
    internal static async Task<(EmbedProperties? Embed, string? Erro)> ProcurarAsync(
        IMyAnimeList mal,
        GoogleTranslator tradutor,
        TipoDeObra tipo,
        string busca,
        string quemPediu,
        CancellationToken cancellationToken = default)
    {
        busca = busca.Trim();

        if (busca.Length < BuscaMínima)
            return (null, Denia.MalBuscaCurta(BuscaMínima));

        IReadOnlyList<Obra> resultados;

        try
        {
            resultados = await mal.ProcurarAsync(tipo, busca, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // só a API oficial recusa credencial; o Jikan nem pede
            return (null, Denia.MalCredencialRecusada());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (null, Denia.MalFora());
        }

        if (Escolhe(resultados, busca) is not { } obra)
            return (null, Denia.MalNãoAchei(tipo, busca));

        var (sinopse, traduzida) = await SinopseAsync(tradutor, obra.Sinopse, cancellationToken);

        return (Embed(obra, sinopse, traduzida, mal.Nome, quemPediu), null);
    }

    /// <summary>
    /// O primeiro resultado costuma ser o certo, mas "naruto" também devolve "Naruto:
    /// Shippuuden" e "Boruto" na frente — se algum título bate exato com a busca, é esse.
    /// </summary>
    private static Obra? Escolhe(IReadOnlyList<Obra> resultados, string busca)
    {
        foreach (var obra in resultados)
        {
            if (Títulos(obra).Any(título => string.Equals(título, busca, StringComparison.OrdinalIgnoreCase)))
                return obra;
        }

        return resultados.Count > 0 ? resultados[0] : null;
    }

    private static IEnumerable<string> Títulos(Obra obra)
    {
        yield return obra.Título;

        if (obra.TítuloEmInglês is { Length: > 0 } inglês)
            yield return inglês;

        if (obra.TítuloEmJaponês is { Length: > 0 } japonês)
            yield return japonês;

        foreach (var sinônimo in obra.Sinônimos)
            yield return sinônimo;
    }

    /// <summary>A sinopse pronta pra mostrar e se ela chegou a ser traduzida. Sem sinopse, null.</summary>
    private static async Task<(string? Texto, bool Traduzida)> SinopseAsync(
        GoogleTranslator tradutor, string? original, CancellationToken cancellationToken)
    {
        var limpa = Limpa(original);

        if (limpa is null)
            return (null, false);

        var traduzida = await tradutor.TraduzirAsync(limpa, cancellationToken: cancellationToken);

        return traduzida is null ? (limpa, false) : (traduzida, true);
    }

    private static string? Limpa(string? sinopse)
    {
        if (string.IsNullOrWhiteSpace(sinopse))
            return null;

        var texto = AssinaturaDoMal.Replace(sinopse, "\n\n").Trim();

        return texto.Length > 0 ? texto : null;
    }

    // ---- embed ---------------------------------------------------------------

    private static EmbedProperties Embed(Obra obra, string? sinopse, bool traduzida, string fonte, string quemPediu)
    {
        var embed = new EmbedProperties
        {
            Title = Recorta(Denia.MalTítulo(obra.Tipo, obra.Título), MaxTítulo),
            Url = obra.Link,
            Description = Descrição(obra, sinopse, traduzida),
            Color = new Color(Azul),
            Fields =
            [
                Campo(Denia.MalCampoNota, Denia.MalNota(obra.Nota, obra.Votos)),
                Campo(Denia.MalCampoRanking, Denia.MalRanking(obra.Posição, obra.Popularidade)),
                Campo(Denia.MalCampoSituação, Denia.MalSituação(obra.Tipo, obra.Situação)),
                Campo(Denia.MalCampoFormato(obra.Tipo), Formato(obra)),
                Campo(Denia.MalCampoPeríodo(obra.Tipo), Denia.MalPeríodo(obra.Início, obra.Fim, obra.Situação)),
                Campo(Denia.MalCampoAutoria(obra.Tipo), Lista(obra.Autoria)),
                Campo(Denia.MalCampoGêneros, Lista(obra.Gêneros.Select(Denia.MalGênero)), inline: false),
            ],
            Footer = new EmbedFooterProperties { Text = Denia.MalRodapé(fonte, quemPediu, traduzida) },
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (obra.Imagem is { } capa)
            embed.Thumbnail = new EmbedThumbnailProperties(capa);

        return embed;
    }

    /// <summary>Os outros nomes em cima, a sinopse embaixo, e o aviso quando ela ficou em inglês.</summary>
    private static string Descrição(Obra obra, string? sinopse, bool traduzida)
    {
        var texto = new StringBuilder();

        if (OutrosNomes(obra) is { } outros)
            texto.Append(Denia.MalTambémConhecida(outros)).Append("\n\n");

        if (sinopse is null)
        {
            texto.Append(Denia.MalSemSinopse());
            return texto.ToString();
        }

        var corpo = Markdown.Escapa(sinopse);

        if (corpo.Length > MaxSinopse)
            texto.Append(CortaNoParágrafo(corpo)).Append("\n\n").Append(Denia.MalSinopseCortada());
        else
            texto.Append(corpo);

        if (!traduzida)
            texto.Append("\n\n").Append(Denia.MalSinopseEmInglês());

        return texto.ToString();
    }

    /// <summary>O título em inglês e o japonês, quando existem e não são o principal de novo.</summary>
    private static string? OutrosNomes(Obra obra)
    {
        var nomes = new[] { obra.TítuloEmInglês, obra.TítuloEmJaponês }
            .Where(nome => !string.IsNullOrWhiteSpace(nome))
            .Select(nome => nome!.Trim())
            .Where(nome => !string.Equals(nome, obra.Título, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(nome => $"**{Markdown.Escapa(nome)}**")
            .ToList();

        return nomes.Count > 0 ? string.Join(" · ", nomes) : null;
    }

    /// <summary>"TV · 220 episódios · 13+", pulando o que o MAL não sabe.</summary>
    private static string Formato(Obra obra)
    {
        var partes = new[]
        {
            Denia.MalFormato(obra.Formato),
            Denia.MalQuantidade(obra.Tipo, obra.Episódios, obra.Capítulos, obra.Volumes),
            Denia.MalClassificação(obra.Classificação),
        };

        var texto = string.Join(" · ", partes.Where(parte => !string.IsNullOrEmpty(parte)));

        return texto.Length > 0 ? texto : "—";
    }

    private static string Lista(IEnumerable<string> itens)
    {
        var texto = string.Join(" · ", itens.Select(item => Markdown.Escapa(item)));

        return texto.Length > 0 ? Recorta(texto, MaxCampo) : "—";
    }

    private static EmbedFieldProperties Campo(string nome, string valor, bool inline = true) =>
        new() { Name = nome, Value = valor, Inline = inline };

    // ---- texto ---------------------------------------------------------------

    /// <summary>
    /// Corta no fim do último parágrafo inteiro que cabe; sem parágrafo, no fim da última
    /// frase. Sinopse cortada no meio da palavra parece erro, não limite.
    /// </summary>
    private static string CortaNoParágrafo(string texto)
    {
        var corte = texto.LastIndexOf("\n\n", MaxSinopse, StringComparison.Ordinal);

        if (corte < MaxSinopse / 2)
            corte = texto.LastIndexOf(". ", MaxSinopse, StringComparison.Ordinal) + 1;

        if (corte < MaxSinopse / 2)
            corte = MaxSinopse;

        return texto[..corte].TrimEnd();
    }

    /// <summary>Corte seco com reticência, pra campo curto que estourou o teto do Discord.</summary>
    private static string Recorta(string texto, int max) =>
        texto.Length <= max ? texto : $"{texto[..(max - 1)]}…";

    private static string SemAcentos(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var saída = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                saída.Append(c);
        }

        return saída.ToString().Normalize(NormalizationForm.FormC);
    }
}

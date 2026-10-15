using System.Globalization;
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

// /ma anime, /ma manga — a ficha do MyAnimeList, com a sinopse traduzida.
[SlashCommand("ma", "Procura um anime ou mangá no MyAnimeList.")]
public sealed class MyAnimelistCommand : AlephSlashModule
{
    private readonly IMyAnimeList _mal;
    private readonly GoogleTranslator _tradutor;
    private readonly IFichasGuardadas _guarda;
    private readonly ISinopseEmPortuguês _mangadex;

    public MyAnimelistCommand(
        IMyAnimeList mal, GoogleTranslator tradutor, IFichasGuardadas guarda, ISinopseEmPortuguês mangadex)
    {
        _mal = mal;
        _tradutor = tradutor;
        _guarda = guarda;
        _mangadex = mangadex;
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
        // perguntar ao MAL e ao tradutor passa dos 3 segundos do Discord; peço tempo
        await DeferAsync();

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            _mal, _tradutor, _guarda, _mangadex, tipo, nome, Context.User.Username);

        if (erro is not null)
            await EditarComErroAsync(erro);
        else
            await EditarRespostaAsync(embed!);
    }
}

// !ma anime/mangá — a mesma ficha do /ma, para quem prefere o prefixo.
public sealed class MyAnimelistTextCommand : AlephTextModule
{
    private readonly IMyAnimeList _mal;
    private readonly GoogleTranslator _tradutor;
    private readonly IFichasGuardadas _guarda;
    private readonly ISinopseEmPortuguês _mangadex;

    public MyAnimelistTextCommand(
        IMyAnimeList mal, GoogleTranslator tradutor, IFichasGuardadas guarda, ISinopseEmPortuguês mangadex)
    {
        _mal = mal;
        _tradutor = tradutor;
        _guarda = guarda;
        _mangadex = mangadex;
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

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            _mal, _tradutor, _guarda, _mangadex, tipoDeObra, nome, Context.User.Username);

        if (erro is not null)
            await ErrorAsync(erro);
        else
            await ReplyAsync(embed!);
    }
}

internal static class MyAnimelist
{
    internal const string Uso = "ma <anime|mangá> <nome>";

    // O MAL não procura por menos que isso: devolve 400 "invalid q".
    internal const int BuscaMínima = 3;

    // Por quanto tempo a ficha guardada responde sozinha; nota e ranking mudam na terceira casa.
    internal static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    // O azul do MyAnimeList.
    private const int Azul = 0x2E51A2;

    // tetos do Discord: 256 no título, 4096 na descrição, 1024 no campo, 6000 no embed
    private const int MaxTítulo = 256;
    private const int MaxSinopse = 3000;
    private const int MaxCampo = 1024;

    // Onde o título acaba e o subtítulo começa, do jeito que o MAL escreve.
    private static readonly char[] Separadores = ['~', ':', '—', '–'];

    // o MAL assina as sinopses que reescreveu; a assinatura não é parte da história
    private static readonly Regex AssinaturaDoMal = new(
        @"\s*\[Written by MAL Rewrite\]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "anime", "mangá", "manga", "Animes"... o que a pessoa digitou no lugar do subcomando.
    internal static TipoDeObra? LerTipo(string texto)
    {
        var limpo = SemAcentos(texto.Trim()).ToLowerInvariant();

        if (limpo.StartsWith("anime", StringComparison.Ordinal))
            return TipoDeObra.Anime;

        if (limpo.StartsWith("manga", StringComparison.Ordinal))
            return TipoDeObra.Manga;

        return null;
    }

    // Procura, escolhe, traduz e monta a ficha. O /ma e o !ma caem os dois aqui.
    internal static async Task<(EmbedProperties? Embed, string? Erro)> ProcurarAsync(
        IMyAnimeList mal,
        GoogleTranslator tradutor,
        IFichasGuardadas guarda,
        ISinopseEmPortuguês mangadex,
        TipoDeObra tipo,
        string busca,
        string quemPediu,
        CancellationToken cancellationToken = default)
    {
        busca = busca.Trim();

        if (busca.Length < BuscaMínima)
            return (null, Denia.MalBuscaCurta(BuscaMínima));

        // o que já se sabe e ainda vale poupa o limite da fonte, que é de todo mundo
        if (await Guardada(guarda, tipo, busca, Validade, cancellationToken) is { } fresca)
            return (await FichaAsync(fresca, tradutor, quemPediu, cancellationToken), null);

        IReadOnlyList<Obra> resultados;

        try
        {
            resultados = await mal.ProcurarAsync(tipo, busca, cancellationToken);

            // o subtítulo colado vem com "~" onde o catálogo usa ":", e a busca não devolve nada
            if (resultados.Count == 0 && SemSubtítulo(busca) is { } começo)
                resultados = await mal.ProcurarAsync(tipo, começo, cancellationToken);
        }
        catch (CredencialRecusadaException)
        {
            // só a API oficial pede chave; quem levanta isto é ela, e não o código HTTP
            return (null, Denia.MalCredencialRecusada());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // a fonte caiu: ficha velha vale mais que embed vermelho, e o rodapé diz a idade
            if (await Guardada(guarda, tipo, busca, validade: null, cancellationToken) is { } velha)
                return (await FichaAsync(velha, tradutor, quemPediu, cancellationToken), null);

            return (null, Denia.MalFora());
        }

        if (Escolhe(resultados, busca) is not { } obra)
            return (null, Denia.MalNãoAchei(tipo, busca));

        // a sinopse em português entra antes de guardar, pro MangaDex não ser incomodado de novo
        obra = await EmPortuguêsAsync(mangadex, obra, cancellationToken);

        await Guardar(guarda, busca, obra, cancellationToken);

        return (await FichaAsync(obra, tradutor, quemPediu, cancellationToken), null);
    }

    // A ficha pronta: a sinopse traduzida e o embed montado em volta dela.
    private static async Task<EmbedProperties> FichaAsync(
        Obra obra, GoogleTranslator tradutor, string quemPediu, CancellationToken cancellationToken)
    {
        var (sinopse, origem) = await SinopseAsync(tradutor, obra, cancellationToken);

        // a fonte sai da obra, e não da cadeia: quem respondeu pode ter sido a reserva
        return Embed(obra, sinopse, origem, obra.Fonte, quemPediu);
    }

    // A obra com a sinopse em português, quando o MangaDex confirma que é a mesma obra.
    private static async Task<Obra> EmPortuguêsAsync(
        ISinopseEmPortuguês mangadex, Obra obra, CancellationToken cancellationToken)
    {
        if (obra.Tipo != TipoDeObra.Manga || obra.SinopseEmPortuguês)
            return obra;

        var português = await mangadex.ParaAsync(obra, cancellationToken);

        return português is null ? obra : obra with { Sinopse = português, SinopseEmPortuguês = true };
    }

    // O banco é conveniência, não dependência: se falhar, a busca segue pela fonte.
    private static async Task<Obra?> Guardada(
        IFichasGuardadas guarda, TipoDeObra tipo, string busca, TimeSpan? validade, CancellationToken cancellationToken)
    {
        if (!guarda.Disponível)
            return null;

        return await guarda.AcharAsync(tipo, busca, validade, cancellationToken);
    }

    private static async Task Guardar(
        IFichasGuardadas guarda, string busca, Obra obra, CancellationToken cancellationToken)
    {
        if (guarda.Disponível)
            await guarda.GuardarAsync(busca, obra, cancellationToken);
    }

    // O que vem antes do subtítulo: "Nome ~o resto~" vira "Nome". Null quando não dá.
    internal static string? SemSubtítulo(string busca)
    {
        busca = busca.Trim();

        var corte = busca.IndexOfAny(Separadores);

        // o hífen só separa quando está solto: "Boukensha-dou" é uma palavra só
        var hífen = busca.IndexOf(" - ", StringComparison.Ordinal);

        if (hífen >= 0 && (corte < 0 || hífen < corte))
            corte = hífen;

        if (corte < 0)
            return null;

        var começo = busca[..corte].Trim();

        return começo.Length >= BuscaMínima && começo.Length < busca.Length ? começo : null;
    }

    // O primeiro resultado costuma ser o certo; título que bate exato com a busca ganha.
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

    // A sinopse pronta pra mostrar e se ela chegou a ser traduzida. Sem sinopse, null.
    private static async Task<(string? Texto, OrigemDaSinopse Origem)> SinopseAsync(
        GoogleTranslator tradutor, Obra obra, CancellationToken cancellationToken)
    {
        var limpa = Limpa(obra.Sinopse);

        if (limpa is null)
            return (null, OrigemDaSinopse.DaFonte);

        // já está em português: passar pelo tradutor seria estragar texto bom
        if (obra.SinopseEmPortuguês)
            return (limpa, obra.Fonte == "MangaDex" ? OrigemDaSinopse.DaFonte : OrigemDaSinopse.DoMangaDex);

        var traduzida = await tradutor.TraduzirAsync(limpa, cancellationToken: cancellationToken);

        return traduzida is null ? (limpa, OrigemDaSinopse.DaFonte) : (traduzida, OrigemDaSinopse.Traduzida);
    }

    private static string? Limpa(string? sinopse)
    {
        if (string.IsNullOrWhiteSpace(sinopse))
            return null;

        var texto = AssinaturaDoMal.Replace(sinopse, "\n\n").Trim();

        return texto.Length > 0 ? texto : null;
    }

    // ---- embed ---------------------------------------------------------------

    private static EmbedProperties Embed(Obra obra, string? sinopse, OrigemDaSinopse origem, string fonte, string quemPediu)
    {
        var embed = new EmbedProperties
        {
            Title = Recorta(Denia.MalTítulo(obra.Tipo, obra.Título), MaxTítulo),
            Url = obra.Link,
            Description = Descrição(obra, sinopse, origem),
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
            Footer = new EmbedFooterProperties { Text = Rodapé(obra, fonte, quemPediu, origem) },
            Timestamp = DateTimeOffset.UtcNow,
        };

        // elenco só de obra que tem elenco: campo vazio é sujeira, não informação
        if (Elenco(obra) is { } elenco)
            embed.Fields = [.. embed.Fields!, Campo(Denia.MalCampoElenco, elenco, inline: false)];

        if (obra.Imagem is { } capa)
            embed.Thumbnail = new EmbedThumbnailProperties(capa);

        return embed;
    }

    // Os personagens numa linha só, principais primeiro, como a fonte devolveu.
    private static string? Elenco(Obra obra)
    {
        if (obra.Personagens.Count == 0)
            return null;

        return Recorta(string.Join(" · ", obra.Personagens.Select(quem => Markdown.Escapa(quem.Nome))), MaxCampo);
    }

    // O rodapé de sempre; ficha do banco ganha a data, pra nota velha não passar por nova.
    private static string Rodapé(Obra obra, string fonte, string quemPediu, OrigemDaSinopse origem)
    {
        var rodapé = Denia.MalRodapé(fonte, quemPediu, origem);

        return obra.VistoEm is { } quando ? $"{rodapé} · {Denia.MalDoCache(quando)}" : rodapé;
    }

    // Os outros nomes em cima, a sinopse embaixo, e o aviso quando ela ficou em inglês.
    private static string Descrição(Obra obra, string? sinopse, OrigemDaSinopse origem)
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

        // o aviso é sobre ter ficado em inglês; português não leva aviso nenhum
        if (origem == OrigemDaSinopse.DaFonte && !obra.SinopseEmPortuguês)
            texto.Append("\n\n").Append(Denia.MalSinopseEmInglês());

        return texto.ToString();
    }

    // O título em inglês e o japonês, quando existem e não são o principal de novo.
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

    // "TV · 220 episódios · 13+", pulando o que o MAL não sabe.
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

    // Corta no fim do último parágrafo que cabe; no meio da palavra parece erro, não limite.
    private static string CortaNoParágrafo(string texto)
    {
        var corte = texto.LastIndexOf("\n\n", MaxSinopse, StringComparison.Ordinal);

        if (corte < MaxSinopse / 2)
            corte = texto.LastIndexOf(". ", MaxSinopse, StringComparison.Ordinal) + 1;

        if (corte < MaxSinopse / 2)
            corte = MaxSinopse;

        return texto[..corte].TrimEnd();
    }

    // Corte seco com reticência, pra campo curto que estourou o teto do Discord.
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

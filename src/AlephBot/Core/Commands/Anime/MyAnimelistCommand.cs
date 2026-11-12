using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AlephBot.Config;
using AlephBot.Core.Commands.Abstractions;
using AlephBot.Core.Commands.Contratos;
using AlephBot.Core.Commands.Registro;
using AlephBot.Core.Personality;
using AlephBot.Threnodian.MyAnimeList;
using AlephBot.Threnodian.Translation;

using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

namespace AlephBot.Core.Commands.Anime;

// /ma — a ficha do anime, com a sinopse traduzida. Mangá é no /mg.
public sealed class MyAnimelistCommand : AlephSlashModule
{
    private readonly IMyAnimeList _mal;
    private readonly GoogleTranslator _tradutor;
    private readonly IFichasGuardadas _guarda;

    public MyAnimelistCommand(IMyAnimeList mal, GoogleTranslator tradutor, IFichasGuardadas guarda)
    {
        _mal = mal;
        _tradutor = tradutor;
        _guarda = guarda;
    }

    public override string Name => "ma";
    public override string Description => "Procura um anime no MyAnimeList.";
    public override CommandCategory Category => CommandCategory.Anime;
    public override string? Usage => MyAnimelist.Uso;

    [SlashCommand("ma", "Procura um anime no MyAnimeList.")]
    public async Task AnimeAsync(
        [SlashCommandParameter(Name = "nome", Description = "O nome do anime.")] string nome)
    {
        // perguntar ao MAL e ao tradutor passa dos 3 segundos do Discord; peço tempo
        await DeferAsync();

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            _mal, _tradutor, _guarda, mangadex: null, TipoDeObra.Anime, nome, Context.User.Username);

        if (erro is not null)
            await EditarComErroAsync(erro);
        else
            await EditarRespostaAsync(embed!);
    }
}

// !ma — a mesma ficha do /ma, para quem prefere o prefixo.
public sealed class MyAnimelistTextCommand : AlephTextModule
{
    private readonly IMyAnimeList _mal;
    private readonly GoogleTranslator _tradutor;
    private readonly IFichasGuardadas _guarda;
    private readonly AlephConfig _config;

    public MyAnimelistTextCommand(
        IMyAnimeList mal, GoogleTranslator tradutor, IFichasGuardadas guarda, AlephConfig config)
    {
        _mal = mal;
        _tradutor = tradutor;
        _guarda = guarda;
        _config = config;
    }

    public override string Name => "ma";
    public override string Description => "Procura um anime no MyAnimeList.";
    public override CommandCategory Category => CommandCategory.Anime;
    public override string? Usage => MyAnimelist.Uso;

    // escondido do /help pra não duplicar a entrada do slash
    public override bool IsHidden => true;

    [Command("ma", "mal", "myanimelist")]
    public async Task MaAsync([CommandParameter(Remainder = true)] string nome)
    {
        var (busca, éMangá) = MyAnimelist.LerPedido(nome);

        // mangá era aqui e agora é no !mg; quem pedir do jeito antigo leva o endereço novo
        if (éMangá)
        {
            await ErrorAsync(Denia.MalMangáÉOutroComando(_config.Prefix, busca));
            return;
        }

        // a resposta demora uns segundos; o "digitando..." é o jeito de dizer que eu ouvi
        await Context.Client.Rest.TriggerTypingAsync(Context.Message.ChannelId);

        var (embed, erro) = await MyAnimelist.ProcurarAsync(
            _mal, _tradutor, _guarda, mangadex: null, TipoDeObra.Anime, busca, Context.User.Username);

        if (erro is not null)
            await ErrorAsync(erro);
        else
            await ReplyAsync(embed!);
    }
}

internal static class MyAnimelist
{
    internal const string Uso = "ma <nome>";

    internal const string UsoDeMangá = "mg <nome>";

    // O MAL não procura por menos que isso: devolve 400 "invalid q".
    internal const int BuscaMínima = 3;

    // Por quanto tempo a ficha guardada responde sozinha; nota e ranking mudam na terceira casa.
    internal static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    // O tempo que a busca inteira tem. As fontes vão em fila e o reteste sem subtítulo roda a
    // fila de novo: sem prazo, somadas, elas deixam a pessoa esperando mais de um minuto.
    internal static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);

    // O azul do MyAnimeList.
    private const int Azul = 0x2E51A2;

    // tetos do Discord: 256 no título, 4096 na descrição, 1024 no campo, 6000 no embed
    private const int MaxTítulo = 256;
    // a capa grande é o que a ficha mostra primeiro; sinopse inteira empurraria ela pro fim do rolo
    private const int MaxSinopse = 500;
    private const int MaxCampo = 1024;

    // Onde o título acaba e o subtítulo começa, do jeito que o MAL escreve.
    private static readonly char[] Separadores = ['~', ':', '—', '–'];

    // o MAL assina as sinopses que reescreveu; a assinatura não é parte da história
    private static readonly Regex AssinaturaDoMal = new(
        @"\s*\[Written by MAL Rewrite\]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // O !ma pedia o tipo na primeira palavra; o /mg existe agora, mas o hábito continua.
    internal static (string Busca, bool ÉMangá) LerPedido(string texto)
    {
        texto = texto.Trim();

        var espaço = texto.IndexOf(' ');

        if (espaço < 0)
            return (texto, false);

        var primeira = SemAcentos(texto[..espaço]).ToLowerInvariant();
        var resto = texto[(espaço + 1)..].Trim();

        if (resto.Length == 0)
            return (texto, false);

        if (primeira.StartsWith("manga", StringComparison.Ordinal))
            return (resto, true);

        // "!ma anime Naruto" continua valendo: a palavra sai e o resto é a busca
        return primeira.StartsWith("anime", StringComparison.Ordinal) ? (resto, false) : (texto, false);
    }

    // Procura, escolhe, traduz e monta a ficha. O /ma e o /mg caem os dois aqui.
    internal static async Task<(EmbedProperties? Embed, string? Erro)> ProcurarAsync(
        IMyAnimeList mal,
        GoogleTranslator tradutor,
        IFichasGuardadas guarda,
        ISinopseEmPortuguês? mangadex,
        TipoDeObra tipo,
        string busca,
        string quemPediu,
        CancellationToken cancellationToken = default)
    {
        busca = busca.Trim();

        if (busca.Length < BuscaMínima)
            return (null, Denia.MalBuscaCurta(BuscaMínima));

        // o prazo é da busca toda, e não de cada fonte: quem espera é uma pessoa, não o código
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        prazo.CancelAfter(Prazo);

        return await MontarAsync(mal, tradutor, guarda, mangadex, tipo, busca, quemPediu, prazo.Token);
    }

    // A busca em si, com o prazo já correndo. Estourado, cai no mesmo caminho de fonte fora.
    private static async Task<(EmbedProperties? Embed, string? Erro)> MontarAsync(
        IMyAnimeList mal,
        GoogleTranslator tradutor,
        IFichasGuardadas guarda,
        ISinopseEmPortuguês? mangadex,
        TipoDeObra tipo,
        string busca,
        string quemPediu,
        CancellationToken cancellationToken)
    {
        // o que já se sabe e ainda vale poupa o limite da fonte, que é de todo mundo
        if (await Guardada(guarda, tipo, busca, Validade, cancellationToken) is { } fresca)
            return (await FichaAsync(fresca, tradutor, quemPediu, cancellationToken), null);

        // a fonte disse agora há pouco que não conhece isto: perguntar de novo gasta o limite dela,
        // que é de todo mundo, e o erro de digitação sai mais rápido pra quem digitou
        if (guarda.JáDeuEmNada(tipo, busca))
            return (null, Denia.MalNãoAchei(tipo, busca));

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
        {
            // fonte viva que não conhece a obra; queda não passa por aqui, e não vira "não existe"
            guarda.DeuEmNada(tipo, busca);

            return (null, Denia.MalNãoAchei(tipo, busca));
        }

        // a sinopse em português entra antes de guardar, pro MangaDex não ser incomodado de novo
        obra = await EmPortuguêsAsync(mangadex, obra, cancellationToken);

        // guardar são quatro idas ao banco e a ficha já está na mão: corre junto com a tradução,
        // em vez de a pessoa esperar uma depois da outra
        var guardando = Guardar(guarda, busca, obra, cancellationToken);
        var ficha = await FichaAsync(obra, tradutor, quemPediu, cancellationToken);

        await guardando;

        return (ficha, null);
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
        ISinopseEmPortuguês? mangadex, Obra obra, CancellationToken cancellationToken)
    {
        // o /ma passa null: anime não tem sinopse em português pra pedir
        if (mangadex is null || obra.Tipo != TipoDeObra.Manga || obra.SinopseEmPortuguês)
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

    // Título igual ganha de sinônimo igual: obra secundária traz o nome da principal e vem antes.
    private static Obra? Escolhe(IReadOnlyList<Obra> resultados, string busca) =>
        Igual(resultados, busca, Títulos)
        ?? Igual(resultados, busca, obra => obra.Sinônimos)
        ?? MaisConhecida(resultados, busca)
        ?? resultados.FirstOrDefault();

    // Busca pela metade ("nanatsu"): entre quem tem as palavras no título, a mais acompanhada.
    private static Obra? MaisConhecida(IReadOnlyList<Obra> resultados, string busca)
    {
        var palavras = SemAcentos(busca).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (palavras.Length == 0)
            return null;

        // sem posição no ranking a obra vai pro fim, e entre iguais fica a ordem da fonte
        return resultados
            .Where(obra => Títulos(obra).Any(título => TemTodas(título, palavras)))
            .OrderBy(obra => obra.Popularidade ?? int.MaxValue)
            .FirstOrDefault();
    }

    private static bool TemTodas(string título, string[] palavras)
    {
        var limpo = SemAcentos(título).ToLowerInvariant();

        return palavras.All(palavra => limpo.Contains(palavra, StringComparison.Ordinal));
    }

    private static Obra? Igual(
        IReadOnlyList<Obra> resultados, string busca, Func<Obra, IEnumerable<string>> nomes) =>
        resultados.FirstOrDefault(
            obra => nomes(obra).Any(nome => string.Equals(nome, busca, StringComparison.OrdinalIgnoreCase)));

    // Os nomes da obra em si; os sinônimos ficam de fora de propósito, ver o Escolhe.
    private static IEnumerable<string> Títulos(Obra obra)
    {
        yield return obra.Título;

        if (obra.TítuloEmInglês is { Length: > 0 } inglês)
            yield return inglês;

        if (obra.TítuloEmJaponês is { Length: > 0 } japonês)
            yield return japonês;
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
                Campo(Denia.MalCampoFormato, Formato(obra)),
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

        // no corpo, e não no canto: a capa vem em extraLarge e num selo de 80px ela se perde
        if (obra.Imagem is { } capa)
            embed.Image = new EmbedImageProperties(capa);

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

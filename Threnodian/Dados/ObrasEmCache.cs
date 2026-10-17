using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using AlephBot.Threnodian.MyAnimeList;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

// As fichas do /ma já mostradas. Sem banco o comando só pergunta à fonte toda vez.
public sealed class ObrasEmCache(
    IDbContextFactory<AlephDb>? fábrica,
    MigracaoDoBanco? banco,
    ILogger<ObrasEmCache> logger) : IFichasGuardadas
{
    // Quanto tempo o "não achei" vale. Curto: obra que estreou hoje tem que aparecer hoje.
    internal static readonly TimeSpan ValidadeDoNada = TimeSpan.FromSeconds(60);

    // Quantos "não achei" eu guardo. A chave é texto digitado: sem teto, spam viraria vazamento.
    private const int NadaNoMáximo = 500;

    // As buscas que não acharam nada, e quando. Em memória de propósito: valendo um minuto, perder
    // isto num restart não custa nada, e o banco não fica com o lixo que alguém digitou.
    private readonly ConcurrentDictionary<(TipoDeObra Tipo, string Termo), DateTimeOffset> _nada = new();

    public bool Disponível => fábrica is not null && banco?.Pronto == true;

    public bool JáDeuEmNada(TipoDeObra tipo, string busca) =>
        JáDeuEmNada(tipo, busca, DateTimeOffset.UtcNow);

    // O agora entra de fora pro teste provar que vence, sem ficar um minuto parado esperando.
    internal bool JáDeuEmNada(TipoDeObra tipo, string busca, DateTimeOffset agora)
    {
        var chave = (tipo, Normaliza(busca));

        if (!_nada.TryGetValue(chave, out var quando))
            return false;

        if (agora - quando < ValidadeDoNada)
            return true;

        _nada.TryRemove(chave, out _);

        return false;
    }

    public void DeuEmNada(TipoDeObra tipo, string busca)
    {
        if (_nada.Count >= NadaNoMáximo)
            TiraOsVencidos();

        // cheio mesmo depois da limpeza: deixo de anotar, que é melhor do que crescer sem fim
        if (_nada.Count >= NadaNoMáximo)
            return;

        _nada[(tipo, Normaliza(busca))] = DateTimeOffset.UtcNow;
    }

    private void TiraOsVencidos()
    {
        var limite = DateTimeOffset.UtcNow - ValidadeDoNada;

        foreach (var (chave, quando) in _nada)
        {
            if (quando < limite)
                _nada.TryRemove(chave, out _);
        }
    }

    public async Task<Obra?> AcharAsync(
        TipoDeObra tipo, string busca, TimeSpan? validade, CancellationToken cancellationToken = default)
    {
        if (!Disponível)
            return null;

        var termo = Normaliza(busca);

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync(cancellationToken);

            var guardada = await PeloTermoAsync(db, tipo, termo, validade, cancellationToken)
                ?? await PeloNomeAsync(db, tipo, termo, validade, cancellationToken);

            return guardada is null ? null : Converte(guardada);
        }
        catch (Exception erro)
        {
            logger.LogWarning(erro, "Não consegui ler a ficha guardada de {Busca}", busca);

            return null;
        }
    }

    // A ficha que alguém já viu procurando exatamente isto.
    private static async Task<ObraGuardada?> PeloTermoAsync(
        AlephDb db, TipoDeObra tipo, string termo, TimeSpan? validade, CancellationToken cancellationToken)
    {
        var buscas = db.Buscas.Where(b => b.Termo == termo && b.Tipo == tipo);

        if (validade is { } prazo)
        {
            var limite = DateTimeOffset.UtcNow - prazo;
            buscas = buscas.Where(b => b.VistoEm >= limite);
        }

        // a ficha só vai virar embed: rastrear a obra e o elenco inteiro é trabalho jogado fora
        var achada = await buscas
            .AsNoTracking()
            .Include(b => b.Obra!)
            .ThenInclude(o => o.Personagens)
            .FirstOrDefaultAsync(cancellationToken);

        return achada?.Obra;
    }

    // A obra pode estar aqui por outro nome. Só vale com a fonte fora: de pé, quem acha é ela.
    private static async Task<ObraGuardada?> PeloNomeAsync(
        AlephDb db, TipoDeObra tipo, string termo, TimeSpan? validade, CancellationToken cancellationToken)
    {
        if (validade is not null)
            return null;

        // Contains viraria strpos, que índice nenhum serve; o trigram atende Like, e só ele
        var padrão = $"%{ParaLike(termo)}%";

        return await db.Obras
            .AsNoTracking()
            .Where(o => o.Tipo == tipo && EF.Functions.Like(o.Procura, padrão))
            .OrderByDescending(o => o.VistoEm)
            .Include(o => o.Personagens)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task GuardarAsync(string busca, Obra obra, CancellationToken cancellationToken = default)
    {
        if (!Disponível)
            return;

        var termo = Normaliza(busca);

        if (termo.Length > BuscaGuardada.TermoNoMaximo)
            termo = termo[..BuscaGuardada.TermoNoMaximo];

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync(cancellationToken);

            var guardada = await db.Obras
                .Include(o => o.Personagens)
                .FirstOrDefaultAsync(
                    o => o.Fonte == obra.Fonte && o.Tipo == obra.Tipo && o.IdNaFonte == obra.Id,
                    cancellationToken);

            if (guardada is null)
            {
                guardada = new ObraGuardada();
                db.Obras.Add(guardada);
            }
            else
            {
                // o elenco é regravado inteiro: comparar um a um custaria mais que refazer
                db.RemoveRange(guardada.Personagens);
                guardada.Personagens.Clear();
            }

            Preenche(guardada, obra);

            await db.SaveChangesAsync(cancellationToken);
            await GuardaOTermoAsync(db, termo, obra.Tipo, guardada.Id, cancellationToken);
        }
        catch (Exception erro)
        {
            // ficha não guardada só custa uma busca a mais amanhã; não vale estragar a resposta
            logger.LogWarning(erro, "Não consegui guardar a ficha de {Busca}", busca);
        }
    }

    private static async Task GuardaOTermoAsync(
        AlephDb db, string termo, TipoDeObra tipo, long obraId, CancellationToken cancellationToken)
    {
        var busca = await db.Buscas.FirstOrDefaultAsync(
            b => b.Termo == termo && b.Tipo == tipo, cancellationToken);

        if (busca is null)
        {
            busca = new BuscaGuardada { Termo = termo, Tipo = tipo };
            db.Buscas.Add(busca);
        }

        busca.ObraId = obraId;
        busca.VistoEm = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- conversão -----------------------------------------------------------

    // A ficha pronta pra gravar; separada pra dar pra conferir a ida e a volta sem banco.
    internal static ObraGuardada Guardada(Obra obra)
    {
        var guardada = new ObraGuardada();

        Preenche(guardada, obra);

        return guardada;
    }

    private static void Preenche(ObraGuardada guardada, Obra obra)
    {
        guardada.Fonte = obra.Fonte;
        guardada.Tipo = obra.Tipo;
        guardada.IdNaFonte = obra.Id;
        guardada.Endereco = obra.Endereço;
        guardada.Titulo = obra.Título;
        guardada.TituloEmIngles = obra.TítuloEmInglês;
        guardada.TituloEmJapones = obra.TítuloEmJaponês;
        guardada.Sinonimos = [.. obra.Sinônimos];
        guardada.Imagem = obra.Imagem;
        guardada.Formato = obra.Formato;
        guardada.Situacao = obra.Situação;
        guardada.Episodios = obra.Episódios;
        guardada.Capitulos = obra.Capítulos;
        guardada.Volumes = obra.Volumes;
        guardada.Inicio = EmTexto(obra.Início);
        guardada.Fim = EmTexto(obra.Fim);
        guardada.Nota = obra.Nota;
        guardada.Votos = obra.Votos;
        guardada.Posicao = obra.Posição;
        guardada.Popularidade = obra.Popularidade;
        guardada.Sinopse = obra.Sinopse;
        guardada.SinopseEmPortugues = obra.SinopseEmPortuguês;
        guardada.Generos = [.. obra.Gêneros];
        guardada.Autoria = [.. obra.Autoria];
        guardada.Classificacao = obra.Classificação;
        guardada.Procura = LinhaDeProcura(obra);
        guardada.VistoEm = DateTimeOffset.UtcNow;

        guardada.Personagens = [.. obra.Personagens
            .Take(ObraGuardada.PersonagensNoMaximo)
            .Select((quem, ordem) => new PersonagemGuardado
            {
                Ordem = ordem,
                Nome = quem.Nome,
                NomeNativo = quem.NomeNativo,
                OutrosNomes = [.. quem.OutrosNomes],
                Principal = quem.Principal,
                Imagem = quem.Imagem,
            })];
    }

    internal static Obra Converte(ObraGuardada guardada) => new()
    {
        Fonte = guardada.Fonte,
        Tipo = guardada.Tipo,
        Id = guardada.IdNaFonte,
        Endereço = guardada.Endereco,
        Título = guardada.Titulo,
        TítuloEmInglês = guardada.TituloEmIngles,
        TítuloEmJaponês = guardada.TituloEmJapones,
        Sinônimos = guardada.Sinonimos,
        Imagem = guardada.Imagem,
        Formato = guardada.Formato,
        Situação = guardada.Situacao,
        Episódios = guardada.Episodios,
        Capítulos = guardada.Capitulos,
        Volumes = guardada.Volumes,
        Início = DataParcial.De(guardada.Inicio),
        Fim = DataParcial.De(guardada.Fim),
        Nota = guardada.Nota,
        Votos = guardada.Votos,
        Posição = guardada.Posicao,
        Popularidade = guardada.Popularidade,
        Sinopse = guardada.Sinopse,
        SinopseEmPortuguês = guardada.SinopseEmPortugues,
        Gêneros = guardada.Generos,
        Autoria = guardada.Autoria,
        Classificação = guardada.Classificacao,
        VistoEm = guardada.VistoEm,

        Personagens = [.. guardada.Personagens
            .OrderBy(quem => quem.Ordem)
            .Select(quem => new Personagem
            {
                Nome = quem.Nome,
                NomeNativo = quem.NomeNativo,
                OutrosNomes = quem.OutrosNomes,
                Principal = quem.Principal,
                Imagem = quem.Imagem,
            })],
    };

    // A data parcial de volta ao texto do MAL: "2002", "2002-10" ou "2002-10-03".
    private static string? EmTexto(DataParcial data) => (data.Ano, data.Mês, data.Dia) switch
    {
        (null, _, _) => null,
        ({ } ano, null, _) => $"{ano:D4}",
        ({ } ano, { } mês, null) => $"{ano:D4}-{mês:D2}",
        ({ } ano, { } mês, { } dia) => $"{ano:D4}-{mês:D2}-{dia:D2}",
    };

    // Todos os nomes numa linha, normalizados: acha pelo título em inglês ou por sinônimo.
    internal static string LinhaDeProcura(Obra obra)
    {
        var nomes = new List<string> { obra.Título };

        if (obra.TítuloEmInglês is { Length: > 0 } inglês)
            nomes.Add(inglês);

        if (obra.TítuloEmJaponês is { Length: > 0 } japonês)
            nomes.Add(japonês);

        nomes.AddRange(obra.Sinônimos);

        return string.Join(" | ", nomes.Select(Normaliza).Distinct());
    }

    // O termo virou padrão de Like: o "%" e o "_" de quem digitou são texto, e não curinga.
    internal static string ParaLike(string termo) => termo
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    // Sem acento, minúsculo e sem espaço sobrando: "NARUTO " e "naruto" são a mesma pergunta.
    internal static string Normaliza(string texto)
    {
        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var saída = new StringBuilder(decomposto.Length);
        var espaço = false;

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(c))
            {
                // "Mangá   do    Berserk" e "Mangá do Berserk" são a mesma pergunta
                espaço = true;
                continue;
            }

            if (espaço && saída.Length > 0)
                saída.Append(' ');

            espaço = false;
            saída.Append(c);
        }

        return saída.ToString().Normalize(NormalizationForm.FormC);
    }
}

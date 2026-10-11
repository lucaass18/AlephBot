using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// Traz pro banco o histórico que ficou na tabela do n8n. Procura um arquivo no volume de
/// dados; achando, lê, grava o que falta e renomeia o arquivo pra não importar duas vezes.
///
/// Este é o único dado daqui que não se refaz: configuração a pessoa digita de novo,
/// playlist se monta outra vez, mas série temporal só existe porque alguém a coletou
/// semana após semana. Por isso nada é apagado e nada é sobrescrito — coleta que já está
/// no banco é pulada pelo horário, e rodar duas vezes não duplica nem perde.
/// </summary>
public sealed class ImportadorDoHistorico(
    IDbContextFactory<AlephDb> fábrica,
    MigracaoDoBanco banco,
    ILogger<ImportadorDoHistorico> logger) : IHostedService
{
    /// <summary>O arquivo exportado do n8n, no mesmo volume onde moram os outros dados.</summary>
    public static readonly string Caminho =
        Path.Combine(AppContext.BaseDirectory, "data", "historico-do-n8n.json");

    private static readonly JsonSerializerOptions Formato = new()
    {
        // as colunas de lá são coletado_em, memoria_mb, na_fila: sem isto só os nomes de
        // uma palavra entram, e os compostos chegam zerados sem ninguém reclamar
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,

        // exportação que passou por CSV traz todo número como texto
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Uma linha como o n8n a guardava: os nomes são os das colunas de lá.</summary>
    public sealed record Linha(
        DateTimeOffset ColetadoEm,
        int Servidores,
        int Membros,
        int Online,
        int Comandos,
        int Players,
        int Tocando,
        int Pausados,
        int NaFila,
        double MemoriaMb,
        double HeapMb);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!banco.Pronto || !File.Exists(Caminho))
            return;

        try
        {
            var lidas = Lê(await File.ReadAllTextAsync(Caminho, cancellationToken));

            if (lidas.Count == 0)
            {
                logger.LogWarning("O arquivo de histórico está vazio ou não é o esperado: {Caminho}", Caminho);
                return;
            }

            var gravadas = await GravaAsync(lidas, cancellationToken);

            // renomeia em vez de apagar: se algo saiu torto, o arquivo ainda está lá
            File.Move(Caminho, $"{Caminho}.importado", overwrite: true);

            logger.LogInformation(
                "Histórico do n8n importado: {Gravadas} coletas novas de {Lidas} no arquivo",
                gravadas,
                lidas.Count);
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Não consegui importar o histórico de {Caminho}", Caminho);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Aceita tanto a lista crua quanto o que a Data table do n8n exporta em volta dela
    /// (um objeto com "data"), porque de onde o arquivo vem depende de como foi exportado.
    /// </summary>
    internal static IReadOnlyList<Linha> Lê(string json)
    {
        var raiz = JsonDocument.Parse(json).RootElement;

        var lista = raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("data", out var dentro)
            ? dentro
            : raiz;

        if (lista.ValueKind != JsonValueKind.Array)
            return [];

        return JsonSerializer.Deserialize<List<Linha>>(lista.GetRawText(), Formato) ?? [];
    }

    /// <summary>Grava só o que o banco ainda não tem, comparando pelo horário da coleta.</summary>
    private async Task<int> GravaAsync(IReadOnlyList<Linha> lidas, CancellationToken cancellationToken)
    {
        await using var db = await fábrica.CreateDbContextAsync(cancellationToken);

        var jáTem = await db.ColetasDeStats
            .Select(c => c.ColetadoEm)
            .ToListAsync(cancellationToken);

        var conhecidas = jáTem.ToHashSet();

        var novas = lidas
            .Where(linha => conhecidas.Add(linha.ColetadoEm))
            .Select(linha => new ColetaDeStats
            {
                ColetadoEm = linha.ColetadoEm,
                Servidores = linha.Servidores,
                Membros = linha.Membros,
                Online = linha.Online,
                Comandos = linha.Comandos,
                Players = linha.Players,
                Tocando = linha.Tocando,
                Pausados = linha.Pausados,
                NaFila = linha.NaFila,
                MemoriaMb = linha.MemoriaMb,
                HeapMb = linha.HeapMb,
            })
            .ToList();

        if (novas.Count == 0)
            return 0;

        db.ColetasDeStats.AddRange(novas);
        await db.SaveChangesAsync(cancellationToken);

        return novas.Count;
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

// Traz o histórico que ficou na tabela do n8n. Série temporal não se refaz: nada é apagado.
public sealed class ImportadorDoHistorico(
    IDbContextFactory<AlephDb> fábrica,
    MigracaoDoBanco banco,
    ILogger<ImportadorDoHistorico> logger) : IHostedService
{
    // O arquivo exportado do n8n, no mesmo volume onde moram os outros dados.
    public static readonly string Caminho =
        Path.Combine(AppContext.BaseDirectory, "data", "historico-do-n8n.json");

    private static readonly JsonSerializerOptions Formato = new()
    {
        // as colunas de lá são coletado_em, memoria_mb, na_fila; sem isto chegam zeradas
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,

        // exportação que passou por CSV traz todo número como texto
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    // Uma linha como o n8n a guardava: os nomes são os das colunas de lá.
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

    // "data" é como o n8n exporta, "historico" é como o backup das 3h guarda; aceito os dois.
    private static readonly string[] OndeAListaMora = ["data", "historico"];

    internal static IReadOnlyList<Linha> Lê(string json)
    {
        var raiz = JsonDocument.Parse(json).RootElement;
        var lista = raiz;

        if (raiz.ValueKind == JsonValueKind.Object)
        {
            foreach (var nome in OndeAListaMora)
            {
                if (raiz.TryGetProperty(nome, out var dentro))
                {
                    lista = dentro;
                    break;
                }
            }
        }

        if (lista.ValueKind != JsonValueKind.Array)
            return [];

        return JsonSerializer.Deserialize<List<Linha>>(lista.GetRawText(), Formato) ?? [];
    }

    // Grava só o que o banco ainda não tem, comparando pelo horário da coleta.
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

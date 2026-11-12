using AlephBot.Threnodian.Api;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

// Anota os números de tempos em tempos, aqui dentro: com o n8n parado a coleta continua.
public sealed class ColetorDeStats(
    IDbContextFactory<AlephDb> fábrica,
    MigracaoDoBanco banco,
    BotStatus bot,
    ILogger<ColetorDeStats> logger) : BackgroundService
{
    // A mesma cadência do fluxo que fazia isto antes, pra linha do tempo não mudar de passo.
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var relógio = new PeriodicTimer(Intervalo);

        while (!stoppingToken.IsCancellationRequested)
        {
            // espera antes da primeira: no boot o cache do gateway ainda está enchendo
            try
            {
                if (!await relógio.WaitForNextTickAsync(stoppingToken))
                    return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await AnotaAsync(stoppingToken);
        }
    }

    private async Task AnotaAsync(CancellationToken cancellationToken)
    {
        if (!banco.Pronto)
            return;

        try
        {
            var números = bot.Estatísticas();

            await using var db = await fábrica.CreateDbContextAsync(cancellationToken);

            db.ColetasDeStats.Add(new ColetaDeStats
            {
                ColetadoEm = DateTimeOffset.UtcNow,
                Servidores = números.Guilds,
                Membros = números.Members,
                Online = números.Online,
                Comandos = números.Commands,
                Players = números.Music.Players,
                Tocando = números.Music.Playing,
                Pausados = números.Music.Paused,
                NaFila = números.Music.QueuedTracks,
                MemoriaMb = números.Memory.WorkingSetMb,
                HeapMb = números.Memory.GcHeapMb,
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // o bot está desligando: não é erro
        }
        catch (Exception erro)
        {
            logger.LogWarning(erro, "Não consegui anotar os números desta vez");
        }
    }
}

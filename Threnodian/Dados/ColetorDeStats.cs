using AlephBot.Threnodian.Api;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// Anota os números do bot de tempos em tempos, direto no banco. Quem fazia isso era um
/// fluxo do n8n, chamando a API de fora a cada quinze minutos; agora a foto é tirada aqui
/// dentro, sem passar por HTTP nem por chave, e continua sendo tirada mesmo com o n8n
/// parado — que foi exatamente o que aconteceu quando os fluxos caíram e ninguém viu.
///
/// Falhar aqui não derruba nada: coleta perdida é buraco na linha do tempo, não bot fora
/// do ar.
/// </summary>
public sealed class ColetorDeStats(
    IDbContextFactory<AlephDb> fábrica,
    MigracaoDoBanco banco,
    BotStatus bot,
    ILogger<ColetorDeStats> logger) : BackgroundService
{
    /// <summary>A mesma cadência do fluxo que fazia isto antes, pra linha do tempo não mudar de passo.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var relógio = new PeriodicTimer(Intervalo);

        while (!stoppingToken.IsCancellationRequested)
        {
            // espera antes da primeira: no boot o cache do gateway ainda está enchendo, e
            // a foto sairia com menos servidores e menos gente do que existe de verdade
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

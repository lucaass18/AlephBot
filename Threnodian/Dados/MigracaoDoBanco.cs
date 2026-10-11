using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// Põe o schema em dia quando o bot sobe. Tenta algumas vezes antes de desistir: o compose
/// já espera o <c>pg_isready</c>, mas depois de reboot da VPS quem sobe os contêineres é a
/// política de restart, sem ordem nenhuma entre eles.
///
/// Desistir aqui não derruba o bot. O banco guarda o que o servidor escolheu; tocar música
/// e responder comando não passa por ele, e um Postgres fora do ar não pode calar o bot
/// inteiro — vira só o padrão do .env valendo pra todo mundo, como era antes de existir
/// banco nenhum.
/// </summary>
public sealed class MigracaoDoBanco(
    IDbContextFactory<AlephDb> fábrica,
    ILogger<MigracaoDoBanco> logger) : IHostedService
{
    private const int Tentativas = 5;

    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(3);

    /// <summary>Se o schema está de pé. O que depende de banco pergunta aqui antes de tentar.</summary>
    public bool Pronto { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            try
            {
                await using var db = await fábrica.CreateDbContextAsync(cancellationToken);
                await db.Database.MigrateAsync(cancellationToken);

                Pronto = true;
                logger.LogInformation("Banco pronto: o schema está em dia");
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception erro) when (tentativa < Tentativas)
            {
                logger.LogWarning(
                    "O banco não respondeu ({Tentativa} de {Total}): {Motivo}. Tento de novo em {Segundos}s",
                    tentativa,
                    Tentativas,
                    erro.Message,
                    Espera.TotalSeconds);

                await Task.Delay(Espera, cancellationToken);
            }
            catch (Exception erro)
            {
                logger.LogError(
                    erro,
                    "O banco não respondeu depois de {Total} tentativas. O bot segue sem ele: "
                    + "cada servidor fica com o padrão do .env até o Postgres voltar",
                    Tentativas);

                return;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using System.Collections.Concurrent;

using AlephBot.Config;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// O que cada servidor escolheu, com o padrão do .env atrás. Quem pergunta nunca recebe
/// null: ou vem o que o servidor escolheu, ou vem o padrão.
///
/// Fica em memória de propósito. O prefixo é lido em toda mensagem de texto que chega, e
/// ir ao banco a cada uma seria uma consulta por mensagem do Discord inteiro — o cache
/// enche sozinho na primeira pergunta de cada servidor e só muda quando alguém escreve.
/// </summary>
public sealed class ConfigDosServidores(
    IDbContextFactory<AlephDb>? fábrica,
    MigracaoDoBanco? banco,
    AlephConfig config,
    ILogger<ConfigDosServidores> logger)
{
    private readonly ConcurrentDictionary<ulong, ConfigDoServidor?> _cache = new();

    /// <summary>Sem banco configurado, ou com ele fora do ar, todo servidor usa o padrão.</summary>
    private bool TemBanco => fábrica is not null && banco?.Pronto == true;

    public async ValueTask<string> PrefixoAsync(ulong guildId) =>
        (await DoServidorAsync(guildId))?.Prefixo ?? config.Prefix;

    public async ValueTask<ulong?> CanalDeLogAsync(ulong guildId) =>
        (await DoServidorAsync(guildId))?.CanalDeLog;

    public async ValueTask<TimeSpan> EsperaDaMúsicaAsync(ulong guildId) =>
        (await DoServidorAsync(guildId))?.MinutosDeEspera is { } minutos
            ? TimeSpan.FromMinutes(minutos)
            : config.MusicIdleTimeout;

    /// <summary>A linha do servidor, ou null se ele nunca mexeu em nada (ou se o banco está fora).</summary>
    public async ValueTask<ConfigDoServidor?> DoServidorAsync(ulong guildId)
    {
        if (_cache.TryGetValue(guildId, out var guardado))
            return guardado;

        if (!TemBanco)
            return null;

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync();

            var achado = await db.ConfigsDosServidores
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.GuildId == guildId);

            _cache[guildId] = achado;
            return achado;
        }
        catch (Exception erro)
        {
            // o banco caiu depois de ter subido: não guardo o null no cache, pra voltar a
            // perguntar quando ele voltar
            logger.LogWarning(erro, "Não consegui ler a configuração do servidor {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    /// Escreve o que mudou e deixa o resto como está. Devolve false quando o banco está
    /// fora — quem chamou avisa a pessoa, em vez de dizer que guardou o que não guardou.
    /// </summary>
    public async Task<bool> SalvaAsync(ulong guildId, Action<ConfigDoServidor> muda)
    {
        if (!TemBanco)
            return false;

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync();

            var linha = await db.ConfigsDosServidores.FirstOrDefaultAsync(c => c.GuildId == guildId);
            var agora = DateTimeOffset.UtcNow;

            if (linha is null)
            {
                linha = new ConfigDoServidor { GuildId = guildId, CriadoEm = agora };
                db.ConfigsDosServidores.Add(linha);
            }

            muda(linha);
            linha.AtualizadoEm = agora;

            await db.SaveChangesAsync();
            _cache[guildId] = linha;

            return true;
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Não consegui guardar a configuração do servidor {GuildId}", guildId);
            return false;
        }
    }

    /// <summary>Esquece o servidor: a próxima pergunta vai ao banco de novo.</summary>
    public void Esquece(ulong guildId) => _cache.TryRemove(guildId, out _);
}

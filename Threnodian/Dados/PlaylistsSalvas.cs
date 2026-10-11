using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// As playlists que os servidores guardaram. Sem banco não há playlist salva — e isso não
/// cala o bot: tocar, pular e a fila do momento nunca passaram por aqui, então o que falta
/// é só a parte de lembrar.
/// </summary>
public sealed class PlaylistsSalvas(
    IDbContextFactory<AlephDb>? fábrica,
    MigracaoDoBanco? banco,
    ILogger<PlaylistsSalvas> logger)
{
    public bool Disponível => fábrica is not null && banco?.Pronto == true;

    /// <summary>O que pode dar errado ao guardar, pra quem chamou saber o que dizer.</summary>
    public enum Resultado
    {
        Guardou,
        SemBanco,
        NomeRepetido,
        ServidorCheio,
    }

    public async Task<IReadOnlyList<PlaylistSalva>> DoServidorAsync(ulong guildId)
    {
        if (!Disponível)
            return [];

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync();

            // sem as faixas: a lista mostra nome, tamanho e quem salvou, e carregar faixa
            // de todas seria trazer o servidor inteiro pra mostrar meia dúzia de linhas
            return await db.Playlists
                .AsNoTracking()
                .Where(p => p.GuildId == guildId)
                .OrderBy(p => p.Nome)
                .Select(p => new PlaylistSalva
                {
                    Id = p.Id,
                    GuildId = p.GuildId,
                    Nome = p.Nome,
                    CriadaPor = p.CriadaPor,
                    CriadaEm = p.CriadaEm,
                    Faixas = p.Faixas.Select(f => new FaixaDaPlaylist { Id = f.Id }).ToList(),
                })
                .ToListAsync();
        }
        catch (Exception erro)
        {
            logger.LogWarning(erro, "Não consegui listar as playlists do servidor {GuildId}", guildId);
            return [];
        }
    }

    /// <summary>A playlist com as faixas na ordem em que foram salvas, ou null se não existe.</summary>
    public async Task<PlaylistSalva?> AcharAsync(ulong guildId, string nome)
    {
        if (!Disponível)
            return null;

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync();

            var playlist = await db.Playlists
                .AsNoTracking()
                .Include(p => p.Faixas)
                .FirstOrDefaultAsync(p => p.GuildId == guildId && p.Nome.ToLower() == nome.ToLower());

            if (playlist is not null)
                playlist.Faixas = [.. playlist.Faixas.OrderBy(f => f.Ordem)];

            return playlist;
        }
        catch (Exception erro)
        {
            logger.LogWarning(erro, "Não consegui abrir a playlist {Nome}", nome);
            return null;
        }
    }

    public async Task<Resultado> GuardaAsync(
        ulong guildId,
        string nome,
        ulong quemSalvou,
        IReadOnlyList<FaixaDaPlaylist> faixas)
    {
        if (!Disponível)
            return Resultado.SemBanco;

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync();

            if (await db.Playlists.AnyAsync(p => p.GuildId == guildId && p.Nome.ToLower() == nome.ToLower()))
                return Resultado.NomeRepetido;

            if (await db.Playlists.CountAsync(p => p.GuildId == guildId) >= PlaylistSalva.PorServidorNoMáximo)
                return Resultado.ServidorCheio;

            db.Playlists.Add(new PlaylistSalva
            {
                GuildId = guildId,
                Nome = nome,
                CriadaPor = quemSalvou,
                CriadaEm = DateTimeOffset.UtcNow,
                Faixas = [.. faixas.Take(PlaylistSalva.FaixasNoMáximo)],
            });

            await db.SaveChangesAsync();
            return Resultado.Guardou;
        }
        catch (DbUpdateException erro)
        {
            // o índice único pegou uma corrida: dois /playlist salvar com o mesmo nome ao
            // mesmo tempo passam os dois pela conferência e só um chega no banco
            logger.LogWarning(erro, "A playlist {Nome} já existia quando fui gravar", nome);
            return Resultado.NomeRepetido;
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Não consegui guardar a playlist {Nome}", nome);
            return Resultado.SemBanco;
        }
    }

    /// <summary>Apaga pelo nome. Devolve false quando não havia nada com esse nome.</summary>
    public async Task<bool> ApagaAsync(ulong guildId, string nome)
    {
        if (!Disponível)
            return false;

        try
        {
            await using var db = await fábrica!.CreateDbContextAsync();

            var quantas = await db.Playlists
                .Where(p => p.GuildId == guildId && p.Nome.ToLower() == nome.ToLower())
                .ExecuteDeleteAsync();

            return quantas > 0;
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Não consegui apagar a playlist {Nome}", nome);
            return false;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlephBot.Threnodian.Dados;

// As playlists guardadas. Sem banco falta só lembrar: tocar e pular não passam por aqui.
public sealed class PlaylistsSalvas(
    IDbContextFactory<AlephDb>? fábrica,
    MigracaoDoBanco? banco,
    ILogger<PlaylistsSalvas> logger)
{
    public bool Disponível => fábrica is not null && banco?.Pronto == true;

    // O que pode dar errado ao guardar, pra quem chamou saber o que dizer.
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

            // sem as faixas: a lista só mostra nome, tamanho e quem salvou
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

    // A playlist com as faixas na ordem em que foram salvas, ou null se não existe.
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
            // o índice único pegou uma corrida: dois /playlist salvar com o mesmo nome
            logger.LogWarning(erro, "A playlist {Nome} já existia quando fui gravar", nome);
            return Resultado.NomeRepetido;
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Não consegui guardar a playlist {Nome}", nome);
            return Resultado.SemBanco;
        }
    }

    // Apaga pelo nome. Devolve false quando não havia nada com esse nome.
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

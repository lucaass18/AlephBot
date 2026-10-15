using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlephBot.Threnodian.Dados;

// Só pro dotnet ef migrations: sem isto o EF subiria o bot inteiro pra achar o contexto.
public sealed class AlephDbDeProjeto : IDesignTimeDbContextFactory<AlephDb>
{
    public AlephDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AlephDb>()
            .UseNpgsql(
                "Host=localhost;Database=aleph;Username=aleph",
                npgsql => npgsql.MigrationsHistoryTable("__migracoes", AlephDb.Schema))
            .Options;

        return new AlephDb(options);
    }
}

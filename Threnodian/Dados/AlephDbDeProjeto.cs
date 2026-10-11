using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// Só pro <c>dotnet ef migrations</c>, na máquina de quem desenvolve. Sem isto o EF tenta
/// subir o bot inteiro pra achar o contexto — e aí ele pede o token do Discord, o Lavalink
/// e o resto, pra gerar um arquivo que não precisa de nada disso.
///
/// A conexão aqui nunca é aberta: gerar migration só lê o modelo. O endereço existe porque
/// o provider exige um, e é por isso que pode ser este, de mentira.
/// </summary>
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

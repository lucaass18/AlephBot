using Microsoft.EntityFrameworkCore;

namespace AlephBot.Threnodian.Dados;

/// <summary>
/// O banco do bot. Mora no schema <c>aleph</c>, do qual ele é dono: o n8n escreve no schema
/// dele e aqui só lê. Dois serviços escrevendo na mesma linha é briga que ninguém ganha, e
/// separar por schema deixa claro de quem é cada tabela sem precisar combinar nada.
/// </summary>
public sealed class AlephDb(DbContextOptions<AlephDb> options) : DbContext(options)
{
    public const string Schema = "aleph";

    /// <summary>Onde o n8n guarda o que é dele. O bot não cria nem mexe em nada aqui.</summary>
    public const string SchemaDoN8n = "n8n";

    public DbSet<ConfigDoServidor> ConfigsDosServidores => Set<ConfigDoServidor>();

    public DbSet<ColetaDeStats> ColetasDeStats => Set<ColetaDeStats>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Schema);

        modelo.Entity<ColetaDeStats>(coleta =>
        {
            coleta.ToTable("stats_historico");
            coleta.HasKey(c => c.Id);

            coleta.Property(c => c.Id).HasColumnName("id");
            coleta.Property(c => c.ColetadoEm).HasColumnName("coletado_em");
            coleta.Property(c => c.Servidores).HasColumnName("servidores");
            coleta.Property(c => c.Membros).HasColumnName("membros");
            coleta.Property(c => c.Online).HasColumnName("online");
            coleta.Property(c => c.Comandos).HasColumnName("comandos");
            coleta.Property(c => c.Players).HasColumnName("players");
            coleta.Property(c => c.Tocando).HasColumnName("tocando");
            coleta.Property(c => c.Pausados).HasColumnName("pausados");
            coleta.Property(c => c.NaFila).HasColumnName("na_fila");
            coleta.Property(c => c.MemoriaMb).HasColumnName("memoria_mb");
            coleta.Property(c => c.HeapMb).HasColumnName("heap_mb");

            // toda leitura do painel é "o que houve entre tal e tal hora", e sem índice
            // isso vira varredura da tabela inteira assim que o histórico encorpar
            coleta.HasIndex(c => c.ColetadoEm).HasDatabaseName("ix_stats_historico_coletado_em");
        });

        modelo.Entity<ConfigDoServidor>(config =>
        {
            config.ToTable("config_do_servidor");
            config.HasKey(c => c.GuildId);

            // o id do Discord é um ulong de 64 bits sem sinal, e o Postgres não tem isso:
            // vai como bigint e volta igual, que é o que o Npgsql faz com a conversão abaixo
            config.Property(c => c.GuildId)
                .HasColumnName("guild_id")
                .HasConversion<long>();

            // o maior prefixo que faz sentido ainda cabe numa palavra; sem limite, um
            // prefixo de mil letras entraria e nenhum comando mais seria reconhecido
            config.Property(c => c.Prefixo).HasColumnName("prefixo").HasMaxLength(16);

            config.Property(c => c.CanalDeLog).HasColumnName("canal_de_log").HasConversion<long?>();
            config.Property(c => c.MinutosDeEspera).HasColumnName("minutos_de_espera");
            config.Property(c => c.CriadoEm).HasColumnName("criado_em");
            config.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        });
    }
}

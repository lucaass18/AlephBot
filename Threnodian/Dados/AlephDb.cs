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

    public DbSet<ConfigDoServidor> ConfigsDosServidores => Set<ConfigDoServidor>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasDefaultSchema(Schema);

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

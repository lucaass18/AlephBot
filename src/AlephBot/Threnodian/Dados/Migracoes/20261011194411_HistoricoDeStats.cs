using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    public partial class HistoricoDeStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // onde o n8n guarda o que é dele; quem cria é o bot, que é quem tem as migrações
            migrationBuilder.EnsureSchema(name: "n8n");

            migrationBuilder.CreateTable(
                name: "stats_historico",
                schema: "aleph",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coletado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    servidores = table.Column<int>(type: "integer", nullable: false),
                    membros = table.Column<int>(type: "integer", nullable: false),
                    online = table.Column<int>(type: "integer", nullable: false),
                    comandos = table.Column<int>(type: "integer", nullable: false),
                    players = table.Column<int>(type: "integer", nullable: false),
                    tocando = table.Column<int>(type: "integer", nullable: false),
                    pausados = table.Column<int>(type: "integer", nullable: false),
                    na_fila = table.Column<int>(type: "integer", nullable: false),
                    memoria_mb = table.Column<double>(type: "double precision", nullable: false),
                    heap_mb = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stats_historico", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stats_historico_coletado_em",
                schema: "aleph",
                table: "stats_historico",
                column: "coletado_em");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // o schema do n8n fica: desfazer isto não é motivo pra apagar o que é de outro serviço
            migrationBuilder.DropTable(
                name: "stats_historico",
                schema: "aleph");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    public partial class ProcuraComTrigram : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // o B-tree nunca serviu "%nome%": a busca por apelido varria a tabela inteira
            migrationBuilder.DropIndex(
                name: "ix_obra_procura",
                schema: "obras",
                table: "obra");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_obra_procura",
                schema: "obras",
                table: "obra",
                column: "procura")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_obra_procura",
                schema: "obras",
                table: "obra");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_obra_procura",
                schema: "obras",
                table: "obra",
                column: "procura");
        }
    }
}

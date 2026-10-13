using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class DivisaoDosSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "obras");

            migrationBuilder.EnsureSchema(
                name: "musica");

            migrationBuilder.EnsureSchema(
                name: "stats");

            migrationBuilder.RenameTable(
                name: "stats_historico",
                schema: "aleph",
                newName: "stats_historico",
                newSchema: "stats");

            migrationBuilder.RenameTable(
                name: "playlist_faixa",
                schema: "aleph",
                newName: "playlist_faixa",
                newSchema: "musica");

            migrationBuilder.RenameTable(
                name: "playlist",
                schema: "aleph",
                newName: "playlist",
                newSchema: "musica");

            migrationBuilder.RenameTable(
                name: "obra_personagem",
                schema: "aleph",
                newName: "obra_personagem",
                newSchema: "obras");

            migrationBuilder.RenameTable(
                name: "obra_busca",
                schema: "aleph",
                newName: "obra_busca",
                newSchema: "obras");

            migrationBuilder.RenameTable(
                name: "obra",
                schema: "aleph",
                newName: "obra",
                newSchema: "obras");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "stats_historico",
                schema: "stats",
                newName: "stats_historico",
                newSchema: "aleph");

            migrationBuilder.RenameTable(
                name: "playlist_faixa",
                schema: "musica",
                newName: "playlist_faixa",
                newSchema: "aleph");

            migrationBuilder.RenameTable(
                name: "playlist",
                schema: "musica",
                newName: "playlist",
                newSchema: "aleph");

            migrationBuilder.RenameTable(
                name: "obra_personagem",
                schema: "obras",
                newName: "obra_personagem",
                newSchema: "aleph");

            migrationBuilder.RenameTable(
                name: "obra_busca",
                schema: "obras",
                newName: "obra_busca",
                newSchema: "aleph");

            migrationBuilder.RenameTable(
                name: "obra",
                schema: "obras",
                newName: "obra",
                newSchema: "aleph");
        }
    }
}

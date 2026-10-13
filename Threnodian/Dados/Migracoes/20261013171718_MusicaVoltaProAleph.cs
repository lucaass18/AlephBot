using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class MusicaVoltaProAleph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // sem CASCADE de propósito: se alguém tiver posto alguma coisa ali, eu prefiro
            // que a migração pare a que ela apague o que não é dela
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS musica RESTRICT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "musica");

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
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    public partial class MusicaVoltaProAleph : Migration
    {
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

            // sem CASCADE de propósito: prefiro a migração parar a ela apagar o que não é dela
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS musica RESTRICT;");
        }

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

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    public partial class SinopseEmPortugues : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "sinopse_em_portugues",
                schema: "aleph",
                table: "obra",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sinopse_em_portugues",
                schema: "aleph",
                table: "obra");
        }
    }
}

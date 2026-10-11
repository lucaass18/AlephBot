using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class Playlists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "playlist",
                schema: "aleph",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guild_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    criada_por = table.Column<long>(type: "bigint", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlist", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "playlist_faixa",
                schema: "aleph",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    playlist_id = table.Column<long>(type: "bigint", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    dados = table.Column<string>(type: "text", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlist_faixa", x => x.id);
                    table.ForeignKey(
                        name: "FK_playlist_faixa_playlist_playlist_id",
                        column: x => x.playlist_id,
                        principalSchema: "aleph",
                        principalTable: "playlist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_guild_nome",
                schema: "aleph",
                table: "playlist",
                columns: new[] { "guild_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_playlist_faixa_ordem",
                schema: "aleph",
                table: "playlist_faixa",
                columns: new[] { "playlist_id", "ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlist_faixa",
                schema: "aleph");

            migrationBuilder.DropTable(
                name: "playlist",
                schema: "aleph");
        }
    }
}

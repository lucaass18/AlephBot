using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlephBot.Threnodian.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class CacheDeObras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "obra",
                schema: "aleph",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fonte = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    id_na_fonte = table.Column<int>(type: "integer", nullable: false),
                    endereco = table.Column<string>(type: "text", nullable: true),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    titulo_em_ingles = table.Column<string>(type: "text", nullable: true),
                    titulo_em_japones = table.Column<string>(type: "text", nullable: true),
                    sinonimos = table.Column<List<string>>(type: "text[]", nullable: false),
                    imagem = table.Column<string>(type: "text", nullable: true),
                    formato = table.Column<string>(type: "text", nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    episodios = table.Column<int>(type: "integer", nullable: true),
                    capitulos = table.Column<int>(type: "integer", nullable: true),
                    volumes = table.Column<int>(type: "integer", nullable: true),
                    inicio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    fim = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    nota = table.Column<double>(type: "double precision", nullable: true),
                    votos = table.Column<int>(type: "integer", nullable: true),
                    posicao = table.Column<int>(type: "integer", nullable: true),
                    popularidade = table.Column<int>(type: "integer", nullable: true),
                    sinopse = table.Column<string>(type: "text", nullable: true),
                    generos = table.Column<List<string>>(type: "text[]", nullable: false),
                    autoria = table.Column<List<string>>(type: "text[]", nullable: false),
                    classificacao = table.Column<string>(type: "text", nullable: true),
                    procura = table.Column<string>(type: "text", nullable: false),
                    visto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_obra", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "obra_busca",
                schema: "aleph",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    termo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    obra_id = table.Column<long>(type: "bigint", nullable: false),
                    visto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_obra_busca", x => x.id);
                    table.ForeignKey(
                        name: "FK_obra_busca_obra_obra_id",
                        column: x => x.obra_id,
                        principalSchema: "aleph",
                        principalTable: "obra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "obra_personagem",
                schema: "aleph",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    obra_id = table.Column<long>(type: "bigint", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    nome_nativo = table.Column<string>(type: "text", nullable: true),
                    outros_nomes = table.Column<List<string>>(type: "text[]", nullable: false),
                    principal = table.Column<bool>(type: "boolean", nullable: false),
                    imagem = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_obra_personagem", x => x.id);
                    table.ForeignKey(
                        name: "FK_obra_personagem_obra_obra_id",
                        column: x => x.obra_id,
                        principalSchema: "aleph",
                        principalTable: "obra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_obra_fonte_tipo_id",
                schema: "aleph",
                table: "obra",
                columns: new[] { "fonte", "tipo", "id_na_fonte" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_obra_procura",
                schema: "aleph",
                table: "obra",
                column: "procura");

            migrationBuilder.CreateIndex(
                name: "IX_obra_busca_obra_id",
                schema: "aleph",
                table: "obra_busca",
                column: "obra_id");

            migrationBuilder.CreateIndex(
                name: "ix_obra_busca_termo_tipo",
                schema: "aleph",
                table: "obra_busca",
                columns: new[] { "termo", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_obra_personagem_obra_id",
                schema: "aleph",
                table: "obra_personagem",
                column: "obra_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "obra_busca",
                schema: "aleph");

            migrationBuilder.DropTable(
                name: "obra_personagem",
                schema: "aleph");

            migrationBuilder.DropTable(
                name: "obra",
                schema: "aleph");
        }
    }
}

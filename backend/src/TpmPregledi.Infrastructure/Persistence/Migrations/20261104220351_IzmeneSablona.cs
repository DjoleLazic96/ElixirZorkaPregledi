using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IzmeneSablona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long[]>(
                name: "uputstvo_slike",
                table: "aktivnost",
                type: "bigint[]",
                nullable: false,
                defaultValue: new long[0]);

            migrationBuilder.CreateTable(
                name: "predlog_izmene",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sablon_id = table.Column<int>(type: "integer", nullable: false),
                    vrsta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stavka_id = table.Column<int>(type: "integer", nullable: true),
                    opis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    razlog = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    podneo_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    odlucio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    odluceno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    odgovor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verzija_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_predlog_izmene", x => x.id);
                    table.ForeignKey(
                        name: "fk_predlog_izmene_sablon_sablon_id",
                        column: x => x.sablon_id,
                        principalTable: "sablon",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_predlog_izmene_sablon_stavka_stavka_id",
                        column: x => x.stavka_id,
                        principalTable: "sablon_stavka",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_predlog_izmene_sablon_verzija_verzija_id",
                        column: x => x.verzija_id,
                        principalTable: "sablon_verzija",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_predlog_izmene_sablon_id_status",
                table: "predlog_izmene",
                columns: new[] { "sablon_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_predlog_izmene_stavka_id",
                table: "predlog_izmene",
                column: "stavka_id");

            migrationBuilder.CreateIndex(
                name: "ix_predlog_izmene_verzija_id",
                table: "predlog_izmene",
                column: "verzija_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "predlog_izmene");

            migrationBuilder.DropColumn(
                name: "uputstvo_slike",
                table: "aktivnost");
        }
    }
}

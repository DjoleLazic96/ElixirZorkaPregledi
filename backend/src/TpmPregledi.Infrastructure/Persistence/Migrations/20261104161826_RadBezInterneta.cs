using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RadBezInterneta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "dugotrajan",
                table: "refresh_token",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "uredjaj_klijenta",
                table: "pregled",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sporan_unos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operacija = table.Column<string>(type: "jsonb", nullable: false),
                    razlog = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    primljeno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    odluka = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sporan_unos", x => x.id);
                    table.ForeignKey(
                        name: "fk_sporan_unos_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sporan_unos_pregled_id",
                table: "sporan_unos",
                column: "pregled_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sporan_unos");

            migrationBuilder.DropColumn(
                name: "dugotrajan",
                table: "refresh_token");

            migrationBuilder.DropColumn(
                name: "uredjaj_klijenta",
                table: "pregled");
        }
    }
}

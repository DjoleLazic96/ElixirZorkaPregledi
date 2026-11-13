using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ZatvaranjeAkcije : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "izvrsilac",
                table: "akcija",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "uradjeno",
                table: "akcija",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "uradjeno_datum",
                table: "akcija",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "zatvaranje_fajl_id",
                table: "akcija",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "zahtev_zatvaranja_akcije",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    akcija_id = table.Column<int>(type: "integer", nullable: false),
                    klijent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    podneo_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uradjeno = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    izvrsilac = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fajl_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    razlog = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    odlucio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    odluceno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zahtev_zatvaranja_akcije", x => x.id);
                    table.ForeignKey(
                        name: "fk_zahtev_zatvaranja_akcije_akcija_akcija_id",
                        column: x => x.akcija_id,
                        principalTable: "akcija",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_zahtev_zatvaranja_akcije_fajl_fajl_id",
                        column: x => x.fajl_id,
                        principalTable: "fajl",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_akcija_zatvaranje_fajl_id",
                table: "akcija",
                column: "zatvaranje_fajl_id");

            migrationBuilder.CreateIndex(
                name: "ix_zahtev_zatvaranja_akcije_akcija_id_status",
                table: "zahtev_zatvaranja_akcije",
                columns: new[] { "akcija_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_zahtev_zatvaranja_akcije_fajl_id",
                table: "zahtev_zatvaranja_akcije",
                column: "fajl_id");

            migrationBuilder.CreateIndex(
                name: "ux_zahtev_zatvaranja_klijent",
                table: "zahtev_zatvaranja_akcije",
                column: "klijent_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_akcija_fajl_zatvaranje_fajl_id",
                table: "akcija",
                column: "zatvaranje_fajl_id",
                principalTable: "fajl",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_akcija_fajl_zatvaranje_fajl_id",
                table: "akcija");

            migrationBuilder.DropTable(
                name: "zahtev_zatvaranja_akcije");

            migrationBuilder.DropIndex(
                name: "ix_akcija_zatvaranje_fajl_id",
                table: "akcija");

            migrationBuilder.DropColumn(
                name: "izvrsilac",
                table: "akcija");

            migrationBuilder.DropColumn(
                name: "uradjeno",
                table: "akcija");

            migrationBuilder.DropColumn(
                name: "uradjeno_datum",
                table: "akcija");

            migrationBuilder.DropColumn(
                name: "zatvaranje_fajl_id",
                table: "akcija");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Zamerke23do37 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "aktivna",
                table: "sekcija",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "propusten",
                table: "pregled",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "original_id",
                table: "predlog_izmene",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pregled_id",
                table: "predlog_izmene",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mora_promeniti_lozinku",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "predlog_saglasnost",
                columns: table => new
                {
                    predlog_id = table.Column<int>(type: "integer", nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_predlog_saglasnost", x => new { x.predlog_id, x.korisnik_id });
                    table.ForeignKey(
                        name: "fk_predlog_saglasnost_predlog_izmene_predlog_id",
                        column: x => x.predlog_id,
                        principalTable: "predlog_izmene",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 1,
                column: "aktivna",
                value: true);

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 2,
                column: "aktivna",
                value: true);

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 3,
                column: "aktivna",
                value: true);

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 4,
                column: "aktivna",
                value: true);

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 5,
                column: "aktivna",
                value: true);

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 6,
                column: "aktivna",
                value: true);

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 7,
                column: "aktivna",
                value: true);

            migrationBuilder.UpdateData(
                table: "sekcija",
                keyColumn: "id",
                keyValue: 8,
                column: "aktivna",
                value: true);

            migrationBuilder.CreateIndex(
                name: "ix_predlog_izmene_original_id",
                table: "predlog_izmene",
                column: "original_id");

            migrationBuilder.CreateIndex(
                name: "ix_predlog_izmene_pregled_id",
                table: "predlog_izmene",
                column: "pregled_id");

            migrationBuilder.AddForeignKey(
                name: "fk_predlog_izmene_predlog_izmene_original_id",
                table: "predlog_izmene",
                column: "original_id",
                principalTable: "predlog_izmene",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_predlog_izmene_pregledi_pregled_id",
                table: "predlog_izmene",
                column: "pregled_id",
                principalTable: "pregled",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_predlog_izmene_predlog_izmene_original_id",
                table: "predlog_izmene");

            migrationBuilder.DropForeignKey(
                name: "fk_predlog_izmene_pregledi_pregled_id",
                table: "predlog_izmene");

            migrationBuilder.DropTable(
                name: "predlog_saglasnost");

            migrationBuilder.DropIndex(
                name: "ix_predlog_izmene_original_id",
                table: "predlog_izmene");

            migrationBuilder.DropIndex(
                name: "ix_predlog_izmene_pregled_id",
                table: "predlog_izmene");

            migrationBuilder.DropColumn(
                name: "aktivna",
                table: "sekcija");

            migrationBuilder.DropColumn(
                name: "propusten",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "original_id",
                table: "predlog_izmene");

            migrationBuilder.DropColumn(
                name: "pregled_id",
                table: "predlog_izmene");

            migrationBuilder.DropColumn(
                name: "mora_promeniti_lozinku",
                table: "AspNetUsers");
        }
    }
}

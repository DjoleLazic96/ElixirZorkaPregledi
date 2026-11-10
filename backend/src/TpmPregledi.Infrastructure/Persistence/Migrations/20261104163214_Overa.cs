using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Overa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "overeno_at",
                table: "pregled",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "overio_direktor",
                table: "pregled",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "overio_id",
                table: "pregled",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ponisten_razlog",
                table: "pregled",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ponistio_id",
                table: "pregled",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "zahtev_dorade",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    podneo_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    razlog = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    odlucio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    odluceno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zahtev_dorade", x => x.id);
                    table.ForeignKey(
                        name: "fk_zahtev_dorade_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pregled_status",
                table: "pregled",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_zahtev_dorade_pregled_id",
                table: "zahtev_dorade",
                column: "pregled_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zahtev_dorade");

            migrationBuilder.DropIndex(
                name: "ix_pregled_status",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "overeno_at",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "overio_direktor",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "overio_id",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "ponisten_razlog",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "ponistio_id",
                table: "pregled");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Obavestenja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "obavestenje",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    vrsta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    naslov = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tekst = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    kljuc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    procitano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isporuceno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pokusaja = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obavestenje", x => x.id);
                    table.ForeignKey(
                        name: "fk_obavestenje_asp_net_users_korisnik_id",
                        column: x => x.korisnik_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_pretplata",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    uredjaj_klijenta = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_pretplata", x => x.id);
                    table.ForeignKey(
                        name: "fk_push_pretplata_asp_net_users_korisnik_id",
                        column: x => x.korisnik_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_obavestenje_korisnik_id_kreirano_at",
                table: "obavestenje",
                columns: new[] { "korisnik_id", "kreirano_at" });

            migrationBuilder.CreateIndex(
                name: "ix_obavestenje_neisporuceno",
                table: "obavestenje",
                column: "isporuceno_at",
                filter: "isporuceno_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_obavestenje_kljuc",
                table: "obavestenje",
                columns: new[] { "korisnik_id", "kljuc" },
                unique: true,
                filter: "kljuc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_push_pretplata_korisnik_id",
                table: "push_pretplata",
                column: "korisnik_id");

            migrationBuilder.CreateIndex(
                name: "ux_push_pretplata_endpoint",
                table: "push_pretplata",
                column: "endpoint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "obavestenje");

            migrationBuilder.DropTable(
                name: "push_pretplata");
        }
    }
}

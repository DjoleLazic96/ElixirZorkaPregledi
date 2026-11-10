using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Osnova : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brojac",
                columns: table => new
                {
                    vrsta = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    godina = table.Column<int>(type: "integer", nullable: false),
                    poslednji = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brojac", x => new { x.vrsta, x.godina });
                });

            migrationBuilder.CreateTable(
                name: "korisnik_pravo",
                columns: table => new
                {
                    korisnik_id = table.Column<string>(type: "text", nullable: false),
                    pravo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_korisnik_pravo", x => new { x.korisnik_id, x.pravo });
                    table.ForeignKey(
                        name: "fk_korisnik_pravo_asp_net_users_korisnik_id",
                        column: x => x.korisnik_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "podesavanje",
                columns: table => new
                {
                    kljuc = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    vrednost = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_podesavanje", x => x.kljuc);
                });

            migrationBuilder.CreateTable(
                name: "sekcija",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sifra = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    naziv = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    redosled = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sekcija", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "sekcija",
                columns: new[] { "id", "naziv", "redosled", "sifra" },
                values: new object[,]
                {
                    { 1, "Sekcija 10", 1, "10" },
                    { 2, "Sekcija 20", 2, "20" },
                    { 3, "Sekcija 21", 3, "21" },
                    { 4, "Sekcija 30", 4, "30" },
                    { 5, "Sekcija 40", 5, "40" },
                    { 6, "Sekcija 50", 6, "50" },
                    { 7, "Sekcija 60", 7, "60" },
                    { 8, "Sekcija 70", 8, "70" }
                });

            migrationBuilder.CreateIndex(
                name: "ux_sekcija_sifra",
                table: "sekcija",
                column: "sifra",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brojac");

            migrationBuilder.DropTable(
                name: "korisnik_pravo");

            migrationBuilder.DropTable(
                name: "podesavanje");

            migrationBuilder.DropTable(
                name: "sekcija");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OpremaISabloni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kategorija");

            migrationBuilder.CreateTable(
                name: "aktivnost",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    naziv = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    odgovor_ispravan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    odgovor_neispravan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    uputstvo_tekst = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    uputstvo_azurirao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    uputstvo_azurirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aktivnost", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dodatno_polje",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    naziv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    jedinica = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    oznake = table.Column<string[]>(type: "text[]", nullable: false),
                    prikaz = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    obavezno = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dodatno_polje", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fajl",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    putanja = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    velicina = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fajl", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sablon",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tip = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sablon", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "uredjaj",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    naziv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    oznaka = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sekcija_id = table.Column<int>(type: "integer", nullable: false),
                    vrsta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    qr_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_uredjaj", x => x.id);
                    table.ForeignKey(
                        name: "fk_uredjaj_sekcija_sekcija_id",
                        column: x => x.sekcija_id,
                        principalTable: "sekcija",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sablon_dogadjaj",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sablon_id = table.Column<int>(type: "integer", nullable: false),
                    verzija_id = table.Column<int>(type: "integer", nullable: true),
                    vrsta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    opis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sablon_dogadjaj", x => x.id);
                    table.ForeignKey(
                        name: "fk_sablon_dogadjaj_sablon_sablon_id",
                        column: x => x.sablon_id,
                        principalTable: "sablon",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sablon_verzija",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sablon_id = table.Column<int>(type: "integer", nullable: false),
                    broj = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vazi_od = table.Column<DateOnly>(type: "date", nullable: true),
                    objavio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    objavljeno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sablon_verzija", x => x.id);
                    table.ForeignKey(
                        name: "fk_sablon_verzija_sablon_sablon_id",
                        column: x => x.sablon_id,
                        principalTable: "sablon",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sablon_stavka",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    verzija_id = table.Column<int>(type: "integer", nullable: false),
                    sekcija_id = table.Column<int>(type: "integer", nullable: false),
                    uredjaj_id = table.Column<int>(type: "integer", nullable: false),
                    aktivnost_id = table.Column<int>(type: "integer", nullable: false),
                    redosled = table.Column<int>(type: "integer", nullable: false),
                    kljuc = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sablon_stavka", x => x.id);
                    table.ForeignKey(
                        name: "fk_sablon_stavka_aktivnost_aktivnost_id",
                        column: x => x.aktivnost_id,
                        principalTable: "aktivnost",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sablon_stavka_sablon_verzija_verzija_id",
                        column: x => x.verzija_id,
                        principalTable: "sablon_verzija",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sablon_stavka_sekcija_sekcija_id",
                        column: x => x.sekcija_id,
                        principalTable: "sekcija",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sablon_stavka_uredjaj_uredjaj_id",
                        column: x => x.uredjaj_id,
                        principalTable: "uredjaj",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sablon_stavka_polje",
                columns: table => new
                {
                    stavka_id = table.Column<int>(type: "integer", nullable: false),
                    dodatno_polje_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sablon_stavka_polje", x => new { x.stavka_id, x.dodatno_polje_id });
                    table.ForeignKey(
                        name: "fk_sablon_stavka_polje_dodatno_polje_dodatno_polje_id",
                        column: x => x.dodatno_polje_id,
                        principalTable: "dodatno_polje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sablon_stavka_polje_sablon_stavka_stavka_id",
                        column: x => x.stavka_id,
                        principalTable: "sablon_stavka",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "dodatno_polje",
                columns: new[] { "id", "created_at", "created_by", "jedinica", "naziv", "obavezno", "oznake", "prikaz", "tip", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), "system", "kom.", "Broj rolni koje ne okreću", false, new string[0], "uz_neispravan", "broj", null, null },
                    { 2, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), "system", "°C", "Temperatura ležaja", false, new[] { "ležaj do rotora", "ležaj do remenice/spojnice" }, "uz_neispravan", "dve_vrednosti", null, null },
                    { 3, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), "system", "kom.", "Broj nedostajućih / oštećenih komada", false, new string[0], "uz_neispravan", "broj", null, null }
                });

            migrationBuilder.InsertData(
                table: "sablon",
                columns: new[] { "id", "tip" },
                values: new object[,]
                {
                    { 1, "DP1" },
                    { 2, "DP2" },
                    { 3, "PZ1" },
                    { 4, "PZ2" },
                    { 5, "PZ3" },
                    { 6, "PZ4" }
                });

            migrationBuilder.CreateIndex(
                name: "ux_aktivnost_naziv_odgovori",
                table: "aktivnost",
                columns: new[] { "naziv", "odgovor_ispravan", "odgovor_neispravan" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_dodatno_polje_naziv",
                table: "dodatno_polje",
                column: "naziv",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sablon_tip",
                table: "sablon",
                column: "tip",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sablon_dogadjaj_sablon_id_at",
                table: "sablon_dogadjaj",
                columns: new[] { "sablon_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_sablon_stavka_aktivnost_id",
                table: "sablon_stavka",
                column: "aktivnost_id");

            migrationBuilder.CreateIndex(
                name: "ix_sablon_stavka_kljuc",
                table: "sablon_stavka",
                column: "kljuc");

            migrationBuilder.CreateIndex(
                name: "ix_sablon_stavka_sekcija_id",
                table: "sablon_stavka",
                column: "sekcija_id");

            migrationBuilder.CreateIndex(
                name: "ix_sablon_stavka_uredjaj_id",
                table: "sablon_stavka",
                column: "uredjaj_id");

            migrationBuilder.CreateIndex(
                name: "ux_sablon_stavka_kljuc",
                table: "sablon_stavka",
                columns: new[] { "verzija_id", "kljuc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sablon_stavka_uredjaj_aktivnost",
                table: "sablon_stavka",
                columns: new[] { "verzija_id", "uredjaj_id", "aktivnost_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sablon_stavka_polje_dodatno_polje_id",
                table: "sablon_stavka_polje",
                column: "dodatno_polje_id");

            migrationBuilder.CreateIndex(
                name: "ux_sablon_verzija_broj",
                table: "sablon_verzija",
                columns: new[] { "sablon_id", "broj" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_uredjaj_sekcija_id",
                table: "uredjaj",
                column: "sekcija_id");

            migrationBuilder.CreateIndex(
                name: "ux_uredjaj_naziv_oznaka",
                table: "uredjaj",
                columns: new[] { "naziv", "oznaka" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_uredjaj_qr_token",
                table: "uredjaj",
                column: "qr_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fajl");

            migrationBuilder.DropTable(
                name: "sablon_dogadjaj");

            migrationBuilder.DropTable(
                name: "sablon_stavka_polje");

            migrationBuilder.DropTable(
                name: "dodatno_polje");

            migrationBuilder.DropTable(
                name: "sablon_stavka");

            migrationBuilder.DropTable(
                name: "aktivnost");

            migrationBuilder.DropTable(
                name: "sablon_verzija");

            migrationBuilder.DropTable(
                name: "uredjaj");

            migrationBuilder.DropTable(
                name: "sablon");

            migrationBuilder.CreateTable(
                name: "kategorija",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    aktivan = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    naziv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    opis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sifra = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kategorija", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_kategorija_sifra",
                table: "kategorija",
                column: "sifra",
                unique: true,
                filter: "aktivan = true");
        }
    }
}

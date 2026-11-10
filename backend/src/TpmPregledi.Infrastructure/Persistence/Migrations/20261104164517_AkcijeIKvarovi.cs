using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AkcijeIKvarovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "akcija",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    broj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    uredjaj_id = table.Column<int>(type: "integer", nullable: false),
                    aktivnost_id = table.Column<int>(type: "integer", nullable: false),
                    stavka_kljuc = table.Column<Guid>(type: "uuid", nullable: false),
                    vrsta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hitnost = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    rok = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    zaduzen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kreirao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    zatvorio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    zatvoreno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    razlog = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stavka_sada_ispravna = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_akcija", x => x.id);
                    table.ForeignKey(
                        name: "fk_akcija_aktivnost_aktivnost_id",
                        column: x => x.aktivnost_id,
                        principalTable: "aktivnost",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_akcija_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_akcija_uredjaj_uredjaj_id",
                        column: x => x.uredjaj_id,
                        principalTable: "uredjaj",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kvar",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    broj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uredjaj_id = table.Column<int>(type: "integer", nullable: false),
                    aktivnost_id = table.Column<int>(type: "integer", nullable: false),
                    stavka_kljuc = table.Column<Guid>(type: "uuid", nullable: false),
                    otvorio_pregled_id = table.Column<int>(type: "integer", nullable: false),
                    zatvorio_pregled_id = table.Column<int>(type: "integer", nullable: true),
                    poslednji_pregled_id = table.Column<int>(type: "integer", nullable: false),
                    otvoren = table.Column<DateOnly>(type: "date", nullable: false),
                    zatvoren = table.Column<DateOnly>(type: "date", nullable: true),
                    prijava = table.Column<int>(type: "integer", nullable: false),
                    poslednja_napomena = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kvar", x => x.id);
                    table.ForeignKey(
                        name: "fk_kvar_aktivnost_aktivnost_id",
                        column: x => x.aktivnost_id,
                        principalTable: "aktivnost",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kvar_pregled_otvorio_pregled_id",
                        column: x => x.otvorio_pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kvar_uredjaj_uredjaj_id",
                        column: x => x.uredjaj_id,
                        principalTable: "uredjaj",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "akcija_dogadjaj",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    akcija_id = table.Column<int>(type: "integer", nullable: false),
                    vrsta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tekst = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    pregled_id = table.Column<int>(type: "integer", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_akcija_dogadjaj", x => x.id);
                    table.ForeignKey(
                        name: "fk_akcija_dogadjaj_akcija_akcija_id",
                        column: x => x.akcija_id,
                        principalTable: "akcija",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_akcija_aktivnost_id",
                table: "akcija",
                column: "aktivnost_id");

            migrationBuilder.CreateIndex(
                name: "ix_akcija_pregled_id",
                table: "akcija",
                column: "pregled_id");

            migrationBuilder.CreateIndex(
                name: "ix_akcija_uredjaj_id_status",
                table: "akcija",
                columns: new[] { "uredjaj_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_akcija_broj",
                table: "akcija",
                column: "broj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_akcija_dogadjaj_akcija_id",
                table: "akcija_dogadjaj",
                column: "akcija_id");

            migrationBuilder.CreateIndex(
                name: "ix_kvar_aktivnost_id",
                table: "kvar",
                column: "aktivnost_id");

            migrationBuilder.CreateIndex(
                name: "ix_kvar_otvorio_pregled_id",
                table: "kvar",
                column: "otvorio_pregled_id");

            migrationBuilder.CreateIndex(
                name: "ix_kvar_uredjaj_id_aktivnost_id_zatvoren",
                table: "kvar",
                columns: new[] { "uredjaj_id", "aktivnost_id", "zatvoren" });

            migrationBuilder.CreateIndex(
                name: "ux_kvar_broj",
                table: "kvar",
                column: "broj",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "akcija_dogadjaj");

            migrationBuilder.DropTable(
                name: "kvar");

            migrationBuilder.DropTable(
                name: "akcija");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Popunjavanje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "aktivno_sekundi",
                table: "pregled",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "delova",
                table: "pregled",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "dorada",
                table: "pregled",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "predat_at",
                table: "pregled",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "zapocet_at",
                table: "pregled",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fotografija",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    stavka_id = table.Column<int>(type: "integer", nullable: false),
                    fajl_id = table.Column<long>(type: "bigint", nullable: false),
                    izvor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    klijent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fotografija", x => x.id);
                    table.ForeignKey(
                        name: "fk_fotografija_fajl_fajl_id",
                        column: x => x.fajl_id,
                        principalTable: "fajl",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fotografija_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "odgovor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    stavka_id = table.Column<int>(type: "integer", nullable: false),
                    vrednost = table.Column<short>(type: "smallint", nullable: false),
                    napomena = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    polja = table.Column<string>(type: "jsonb", nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    klijent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    server_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    op_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dorada = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_odgovor", x => x.id);
                    table.ForeignKey(
                        name: "fk_odgovor_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_odgovor_sablon_stavka_stavka_id",
                        column: x => x.stavka_id,
                        principalTable: "sablon_stavka",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sinhronizacija_operacija",
                columns: table => new
                {
                    op_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    vrsta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    klijent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    primljeno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sinhronizacija_operacija", x => x.op_id);
                });

            migrationBuilder.CreateTable(
                name: "uredjaj_van_funkcije",
                columns: table => new
                {
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    uredjaj_id = table.Column<int>(type: "integer", nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    klijent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_uredjaj_van_funkcije", x => new { x.pregled_id, x.uredjaj_id });
                    table.ForeignKey(
                        name: "fk_uredjaj_van_funkcije_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_uredjaj_van_funkcije_uredjaj_uredjaj_id",
                        column: x => x.uredjaj_id,
                        principalTable: "uredjaj",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fotografija_fajl_id",
                table: "fotografija",
                column: "fajl_id");

            migrationBuilder.CreateIndex(
                name: "ix_fotografija_pregled_id_stavka_id",
                table: "fotografija",
                columns: new[] { "pregled_id", "stavka_id" });

            migrationBuilder.CreateIndex(
                name: "ux_fotografija_klijent_id",
                table: "fotografija",
                column: "klijent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_odgovor_stavka_id",
                table: "odgovor",
                column: "stavka_id");

            migrationBuilder.CreateIndex(
                name: "ux_odgovor_stavka_dorada",
                table: "odgovor",
                columns: new[] { "pregled_id", "stavka_id", "dorada" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sinhronizacija_operacija_pregled_id",
                table: "sinhronizacija_operacija",
                column: "pregled_id");

            migrationBuilder.CreateIndex(
                name: "ix_uredjaj_van_funkcije_uredjaj_id",
                table: "uredjaj_van_funkcije",
                column: "uredjaj_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fotografija");

            migrationBuilder.DropTable(
                name: "odgovor");

            migrationBuilder.DropTable(
                name: "sinhronizacija_operacija");

            migrationBuilder.DropTable(
                name: "uredjaj_van_funkcije");

            migrationBuilder.DropColumn(
                name: "aktivno_sekundi",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "delova",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "dorada",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "predat_at",
                table: "pregled");

            migrationBuilder.DropColumn(
                name: "zapocet_at",
                table: "pregled");
        }
    }
}

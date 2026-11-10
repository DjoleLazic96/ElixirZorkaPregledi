using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Raspored : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "neradni_dan",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    naziv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vrsta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uneo_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_neradni_dan", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "odsustvo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    od = table.Column<DateOnly>(type: "date", nullable: false),
                    @do = table.Column<DateOnly>(name: "do", type: "date", nullable: false),
                    vrsta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    napomena = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    zatrazio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    odlucio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    razlog_odbijanja = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    odluceno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_odsustvo", x => x.id);
                    table.ForeignKey(
                        name: "fk_odsustvo_asp_net_users_korisnik_id",
                        column: x => x.korisnik_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pregled",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    broj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tip = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    sablon_verzija_id = table.Column<int>(type: "integer", nullable: true),
                    planirani_datum = table.Column<DateOnly>(type: "date", nullable: false),
                    prvobitni_datum = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dodeljen_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    napomena = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pregled", x => x.id);
                    table.ForeignKey(
                        name: "fk_pregled_asp_net_users_dodeljen_id",
                        column: x => x.dodeljen_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pregled_sablon_verzija_sablon_verzija_id",
                        column: x => x.sablon_verzija_id,
                        principalTable: "sablon_verzija",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pregled_dogadjaj",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    vrsta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    podaci = table.Column<string>(type: "jsonb", nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pregled_dogadjaj", x => x.id);
                    table.ForeignKey(
                        name: "fk_pregled_dogadjaj_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pregled_ucesnik",
                columns: table => new
                {
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    korisnik_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sekcije = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pregled_ucesnik", x => new { x.pregled_id, x.korisnik_id });
                    table.ForeignKey(
                        name: "fk_pregled_ucesnik_asp_net_users_korisnik_id",
                        column: x => x.korisnik_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pregled_ucesnik_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zahtev_prebacivanja",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pregled_id = table.Column<int>(type: "integer", nullable: false),
                    podneo_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    predlozena_zamena_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    razlog = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    zamena_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    odlucio_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    kreirano_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    odluceno_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zahtev_prebacivanja", x => x.id);
                    table.ForeignKey(
                        name: "fk_zahtev_prebacivanja_pregled_pregled_id",
                        column: x => x.pregled_id,
                        principalTable: "pregled",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_neradni_dan_datum",
                table: "neradni_dan",
                column: "datum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_odsustvo_korisnik_id_od",
                table: "odsustvo",
                columns: new[] { "korisnik_id", "od" });

            migrationBuilder.CreateIndex(
                name: "ix_pregled_dodeljen_id",
                table: "pregled",
                column: "dodeljen_id");

            migrationBuilder.CreateIndex(
                name: "ix_pregled_planirani_datum",
                table: "pregled",
                column: "planirani_datum");

            migrationBuilder.CreateIndex(
                name: "ix_pregled_sablon_verzija_id",
                table: "pregled",
                column: "sablon_verzija_id");

            migrationBuilder.CreateIndex(
                name: "ux_pregled_broj",
                table: "pregled",
                column: "broj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pregled_dogadjaj_pregled_id_at",
                table: "pregled_dogadjaj",
                columns: new[] { "pregled_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_pregled_ucesnik_korisnik_id",
                table: "pregled_ucesnik",
                column: "korisnik_id");

            migrationBuilder.CreateIndex(
                name: "ix_zahtev_prebacivanja_pregled_id",
                table: "zahtev_prebacivanja",
                column: "pregled_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "neradni_dan");

            migrationBuilder.DropTable(
                name: "odsustvo");

            migrationBuilder.DropTable(
                name: "pregled_dogadjaj");

            migrationBuilder.DropTable(
                name: "pregled_ucesnik");

            migrationBuilder.DropTable(
                name: "zahtev_prebacivanja");

            migrationBuilder.DropTable(
                name: "pregled");
        }
    }
}

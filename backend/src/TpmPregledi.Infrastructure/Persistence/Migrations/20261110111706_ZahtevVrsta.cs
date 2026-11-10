using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ZahtevVrsta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "vrsta",
                table: "zahtev_prebacivanja",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "prebacivanje");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "vrsta",
                table: "zahtev_prebacivanja");
        }
    }
}

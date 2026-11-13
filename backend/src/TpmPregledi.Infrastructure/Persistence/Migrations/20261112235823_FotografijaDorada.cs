using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TpmPregledi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FotografijaDorada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dorada",
                table: "fotografija",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dorada",
                table: "fotografija");
        }
    }
}

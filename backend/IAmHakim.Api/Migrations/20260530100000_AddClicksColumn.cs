using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAmHakim.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClicksColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Clicks",
                table: "SiteStats",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Clicks",
                table: "SiteStats");
        }
    }
}

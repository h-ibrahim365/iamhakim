using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAmHakim.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Bookings",
                type: "varchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "Bookings");
        }
    }
}

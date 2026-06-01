using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAmHakim.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAntiBotIpAndTurnstileSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpHash",
                table: "Bookings",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IpHash",
                table: "EmailVerifications",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_IpHash_Status_CreatedAtUtc",
                table: "Bookings",
                columns: new[] { "IpHash", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_IpHash_CreatedAtUtc",
                table: "EmailVerifications",
                columns: new[] { "IpHash", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_IpHash_Status_CreatedAtUtc",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_IpHash_CreatedAtUtc",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "IpHash",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IpHash",
                table: "EmailVerifications");
        }
    }
}

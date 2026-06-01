using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAmHakim.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionToken",
                table: "Bookings",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAtUtc",
                table: "Bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "Bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Bookings",
                type: "varchar(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(16)",
                oldMaxLength: 16)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_DecisionToken",
                table: "Bookings",
                column: "DecisionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Email_Status_CreatedAtUtc",
                table: "Bookings",
                columns: new[] { "Email", "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_DecisionToken",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_Email_Status_CreatedAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DecisionToken",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "Bookings");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Bookings",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(24)",
                oldMaxLength: 24)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}

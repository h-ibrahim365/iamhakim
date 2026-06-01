using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IAmHakim.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRescheduleWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedEndUtc",
                table: "Bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedStartUtc",
                table: "Bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RequestedStartUtc",
                table: "Bookings",
                column: "RequestedStartUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_RequestedStartUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RequestedEndUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RequestedStartUtc",
                table: "Bookings");
        }
    }
}

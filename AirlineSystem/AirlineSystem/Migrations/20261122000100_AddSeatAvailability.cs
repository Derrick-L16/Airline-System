using AirlineSystem.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineSystem.Migrations
{
    [DbContext(typeof(DB))]
    [Migration("20261122000100_AddSeatAvailability")]
    public partial class AddSeatAvailability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SeatAvailability",
                table: "Seats",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeatAvailability",
                table: "Seats");
        }
    }
}

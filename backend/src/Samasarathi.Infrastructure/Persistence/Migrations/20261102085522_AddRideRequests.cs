using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samasarathi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRideRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rides_RiderId",
                table: "Rides");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "Rides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Rides",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Rides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rides_OneActivePerDriver",
                table: "Rides",
                column: "DriverId",
                unique: true,
                filter: "\"Status\" IN ('Requested', 'Accepted', 'DriverArrived', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_OneActivePerRider",
                table: "Rides",
                column: "RiderId",
                unique: true,
                filter: "\"Status\" IN ('Requested', 'Accepted', 'DriverArrived', 'InProgress')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rides_OneActivePerDriver",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "IX_Rides_OneActivePerRider",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Rides");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_RiderId",
                table: "Rides",
                column: "RiderId");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samasarathi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRideRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentForDriver",
                table: "Rides",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentForRider",
                table: "Rides",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RatedDriverAt",
                table: "Rides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RatedRiderAt",
                table: "Rides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingForDriver",
                table: "Rides",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingForRider",
                table: "Rides",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rides_RiderId_RequestedAt",
                table: "Rides",
                columns: new[] { "RiderId", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rides_RiderId_RequestedAt",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "CommentForDriver",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "CommentForRider",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "RatedDriverAt",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "RatedRiderAt",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "RatingForDriver",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "RatingForRider",
                table: "Rides");
        }
    }
}

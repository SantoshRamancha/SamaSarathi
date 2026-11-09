using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samasarathi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverLastLocationAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLocationAt",
                table: "Drivers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLocationAt",
                table: "Drivers");
        }
    }
}

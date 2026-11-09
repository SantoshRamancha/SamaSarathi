using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samasarathi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DriverDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverDocuments_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverDocuments_DriverId_Type",
                table: "DriverDocuments",
                columns: new[] { "DriverId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverDocuments");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Drivers");
        }
    }
}

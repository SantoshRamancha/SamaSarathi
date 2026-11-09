using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samasarathi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "VerificationChecks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "VerificationChecks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Drivers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "Drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "Drivers",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            // Checks opened before this column existed belong to the driver's (only) submission.
            migrationBuilder.Sql("""
                UPDATE "VerificationChecks" c
                SET "CreatedAt" = COALESCE(d."SubmittedAt", d."CreatedAt")
                FROM "Drivers" d
                WHERE c."DriverId" = d."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_VerificationStatus",
                table: "Drivers",
                column: "VerificationStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_VerificationStatus",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "VerificationChecks");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "Drivers");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "VerificationChecks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}

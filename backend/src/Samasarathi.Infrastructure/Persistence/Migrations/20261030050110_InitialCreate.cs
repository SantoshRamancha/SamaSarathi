using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Samasarathi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    RatePerKm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastKnownLocation = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Riders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Riders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "text", nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentReference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Model = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VerificationChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Result = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    ProviderReference = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationChecks_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RiderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Pickup = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    Drop = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    RatePerKm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PricingMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EstimatedDistanceKm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EstimatedMinutes = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EstimatedFare = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ActualDistanceKm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ActualMinutes = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    FinalFare = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rides_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Rides_Riders_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Riders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Phone",
                table: "Drivers",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Riders_Phone",
                table: "Riders",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rides_DriverId_Status",
                table: "Rides",
                columns: new[] { "DriverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_RiderId",
                table: "Rides",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DriverId",
                table: "Subscriptions",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DriverId",
                table: "Vehicles",
                column: "DriverId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegistrationNumber",
                table: "Vehicles",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationChecks_DriverId",
                table: "VerificationChecks",
                column: "DriverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rides");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "VerificationChecks");

            migrationBuilder.DropTable(
                name: "Riders");

            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}

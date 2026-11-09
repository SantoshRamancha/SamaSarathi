using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samasarathi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_DriverId",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "PlanName",
                table: "Subscriptions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "GrantedBy",
                table: "Subscriptions",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Subscriptions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Paid");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Subscriptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DriverId_EndsAt",
                table: "Subscriptions",
                columns: new[] { "DriverId", "EndsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_DriverId_EndsAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "GrantedBy",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "PlanName",
                table: "Subscriptions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DriverId",
                table: "Subscriptions",
                column: "DriverId");
        }
    }
}

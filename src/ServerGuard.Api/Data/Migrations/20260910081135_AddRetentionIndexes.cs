using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerGuard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TrafficLogs_CreatedAt",
                table: "TrafficLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMetrics_CreatedAt",
                table: "ServerMetrics",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_CreatedAt",
                table: "SecurityEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAlerts_CreatedAt",
                table: "SecurityAlerts",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrafficLogs_CreatedAt",
                table: "TrafficLogs");

            migrationBuilder.DropIndex(
                name: "IX_ServerMetrics_CreatedAt",
                table: "ServerMetrics");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_CreatedAt",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_SecurityAlerts_CreatedAt",
                table: "SecurityAlerts");
        }
    }
}

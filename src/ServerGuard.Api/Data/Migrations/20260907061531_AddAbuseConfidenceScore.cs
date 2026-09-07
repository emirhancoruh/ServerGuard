using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerGuard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbuseConfidenceScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbuseConfidenceScore",
                table: "SecurityAlerts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbuseConfidenceScore",
                table: "SecurityAlerts");
        }
    }
}

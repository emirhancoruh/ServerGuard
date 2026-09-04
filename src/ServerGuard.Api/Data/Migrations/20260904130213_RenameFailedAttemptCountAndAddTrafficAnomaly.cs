using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerGuard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameFailedAttemptCountAndAddTrafficAnomaly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FailedAttemptCount",
                table: "SecurityAlerts",
                newName: "ObservedCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ObservedCount",
                table: "SecurityAlerts",
                newName: "FailedAttemptCount");
        }
    }
}

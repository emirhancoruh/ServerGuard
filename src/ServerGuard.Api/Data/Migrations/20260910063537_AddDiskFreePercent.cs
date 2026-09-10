using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerGuard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiskFreePercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DiskFreePercent",
                table: "ServerMetrics",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiskFreePercent",
                table: "ServerMetrics");
        }
    }
}

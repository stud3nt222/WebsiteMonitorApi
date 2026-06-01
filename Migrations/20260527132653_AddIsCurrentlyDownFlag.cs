using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebsiteMonitorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCurrentlyDownFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentlyDown",
                table: "websites",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCurrentlyDown",
                table: "websites");
        }
    }
}

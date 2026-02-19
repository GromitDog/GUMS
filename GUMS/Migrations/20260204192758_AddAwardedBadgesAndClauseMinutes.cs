using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardedBadgesAndClauseMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "BadgeDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "BadgeClauses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "BadgeClauses");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "BadgeDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}

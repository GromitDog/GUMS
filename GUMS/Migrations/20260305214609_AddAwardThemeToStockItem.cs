using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardThemeToStockItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwardTheme",
                table: "BadgeStockItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwardTheme",
                table: "BadgeStockItems");
        }
    }
}

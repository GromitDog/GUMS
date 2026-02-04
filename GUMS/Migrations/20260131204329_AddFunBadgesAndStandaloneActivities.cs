using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFunBadgesAndStandaloneActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BadgeDefinitionId",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_BadgeDefinitionId",
                table: "Activities",
                column: "BadgeDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_BadgeDefinitions_BadgeDefinitionId",
                table: "Activities",
                column: "BadgeDefinitionId",
                principalTable: "BadgeDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_BadgeDefinitions_BadgeDefinitionId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_BadgeDefinitionId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "BadgeDefinitionId",
                table: "Activities");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeAccountToMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncomeAccountId",
                table: "Meetings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_IncomeAccountId",
                table: "Meetings",
                column: "IncomeAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Accounts_IncomeAccountId",
                table: "Meetings",
                column: "IncomeAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Accounts_IncomeAccountId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_IncomeAccountId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "IncomeAccountId",
                table: "Meetings");
        }
    }
}

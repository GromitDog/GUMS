using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeAccountToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncomeAccountId",
                table: "Payments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IncomeAccountId",
                table: "Payments",
                column: "IncomeAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Accounts_IncomeAccountId",
                table: "Payments",
                column: "IncomeAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Accounts_IncomeAccountId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_IncomeAccountId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IncomeAccountId",
                table: "Payments");
        }
    }
}

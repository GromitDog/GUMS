using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCostCentres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostCentreId",
                table: "TransactionLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostCentreId",
                table: "Meetings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostCentreId",
                table: "Expenses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CostCentres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCentres", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_CostCentreId",
                table: "TransactionLines",
                column: "CostCentreId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CostCentreId",
                table: "Meetings",
                column: "CostCentreId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CostCentreId",
                table: "Expenses",
                column: "CostCentreId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCentres_IsActive",
                table: "CostCentres",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CostCentres_Name",
                table: "CostCentres",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_CostCentres_CostCentreId",
                table: "Expenses",
                column: "CostCentreId",
                principalTable: "CostCentres",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_CostCentres_CostCentreId",
                table: "Meetings",
                column: "CostCentreId",
                principalTable: "CostCentres",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionLines_CostCentres_CostCentreId",
                table: "TransactionLines",
                column: "CostCentreId",
                principalTable: "CostCentres",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_CostCentres_CostCentreId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_CostCentres_CostCentreId",
                table: "Meetings");

            migrationBuilder.DropForeignKey(
                name: "FK_TransactionLines_CostCentres_CostCentreId",
                table: "TransactionLines");

            migrationBuilder.DropTable(
                name: "CostCentres");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLines_CostCentreId",
                table: "TransactionLines");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_CostCentreId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CostCentreId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CostCentreId",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "CostCentreId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "CostCentreId",
                table: "Expenses");
        }
    }
}

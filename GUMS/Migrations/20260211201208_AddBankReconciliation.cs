using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddBankReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankReconciliationId",
                table: "TransactionLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankReconciliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StatementDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StatementBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReconciledBookBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_BankReconciliationId",
                table: "TransactionLines",
                column: "BankReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_StatementDate",
                table: "BankReconciliations",
                column: "StatementDate");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_Status",
                table: "BankReconciliations",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionLines_BankReconciliations_BankReconciliationId",
                table: "TransactionLines",
                column: "BankReconciliationId",
                principalTable: "BankReconciliations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionLines_BankReconciliations_BankReconciliationId",
                table: "TransactionLines");

            migrationBuilder.DropTable(
                name: "BankReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLines_BankReconciliationId",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "BankReconciliationId",
                table: "TransactionLines");
        }
    }
}

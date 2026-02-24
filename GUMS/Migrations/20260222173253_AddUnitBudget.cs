using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnitBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FinancialYearEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitBudgetItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnitBudgetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    Allocation = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ExpenseAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitBudgetItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitBudgetItems_Accounts_ExpenseAccountId",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitBudgetItems_UnitBudgets_UnitBudgetId",
                        column: x => x.UnitBudgetId,
                        principalTable: "UnitBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitBudgetItems_ExpenseAccountId",
                table: "UnitBudgetItems",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitBudgetItems_UnitBudgetId",
                table: "UnitBudgetItems",
                column: "UnitBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitBudgets_FinancialYearEnd",
                table: "UnitBudgets",
                column: "FinancialYearEnd",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitBudgetItems");

            migrationBuilder.DropTable(
                name: "UnitBudgets");
        }
    }
}

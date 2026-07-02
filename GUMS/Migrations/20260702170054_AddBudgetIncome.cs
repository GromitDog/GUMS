using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetIncome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlannedGirlCount",
                table: "EventBudgets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventBudgetIncomes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventBudgetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventBudgetIncomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventBudgetIncomes_EventBudgets_EventBudgetId",
                        column: x => x.EventBudgetId,
                        principalTable: "EventBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventBudgetIncomes_EventBudgetId",
                table: "EventBudgetIncomes",
                column: "EventBudgetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventBudgetIncomes");

            migrationBuilder.DropColumn(
                name: "PlannedGirlCount",
                table: "EventBudgets");
        }
    }
}

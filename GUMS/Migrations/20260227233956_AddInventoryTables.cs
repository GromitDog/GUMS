using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BadgeStockItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StockType = table.Column<int>(type: "INTEGER", nullable: false),
                    BadgeDefinitionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ThemeAwardLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    NightsAwayTier = table.Column<int>(type: "INTEGER", nullable: true),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    ReorderThreshold = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeStockItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeStockItems_BadgeDefinitions_BadgeDefinitionId",
                        column: x => x.BadgeDefinitionId,
                        principalTable: "BadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BadgeStockTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BadgeStockItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionType = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    TotalCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    AwardedBadgeId = table.Column<int>(type: "INTEGER", nullable: true),
                    AwardedThemeAwardId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExpenseClaimId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeStockTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeStockTransactions_BadgeStockItems_BadgeStockItemId",
                        column: x => x.BadgeStockItemId,
                        principalTable: "BadgeStockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeStockItems_BadgeDefinitionId",
                table: "BadgeStockItems",
                column: "BadgeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeStockItems_IsActive",
                table: "BadgeStockItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeStockItems_StockType",
                table: "BadgeStockItems",
                column: "StockType");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeStockTransactions_BadgeStockItemId",
                table: "BadgeStockTransactions",
                column: "BadgeStockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeStockTransactions_TransactionDate",
                table: "BadgeStockTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeStockTransactions_TransactionType",
                table: "BadgeStockTransactions",
                column: "TransactionType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BadgeStockTransactions");

            migrationBuilder.DropTable(
                name: "BadgeStockItems");
        }
    }
}

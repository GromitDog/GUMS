using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefundFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Payments",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundDate",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundTransactionId",
                table: "Payments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RefundTransactionId",
                table: "Payments",
                column: "RefundTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Transactions_RefundTransactionId",
                table: "Payments",
                column: "RefundTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Transactions_RefundTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RefundTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundTransactionId",
                table: "Payments");
        }
    }
}

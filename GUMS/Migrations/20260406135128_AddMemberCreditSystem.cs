using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberCreditSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditApplied",
                table: "Payments",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CreditTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SourcePaymentId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetPaymentId = table.Column<int>(type: "INTEGER", nullable: true),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_Payments_SourcePaymentId",
                        column: x => x.SourcePaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_Payments_TargetPaymentId",
                        column: x => x.TargetPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MemberCredits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCredits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_Date",
                table: "CreditTransactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_MembershipNumber",
                table: "CreditTransactions",
                column: "MembershipNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_SourcePaymentId",
                table: "CreditTransactions",
                column: "SourcePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_TargetPaymentId",
                table: "CreditTransactions",
                column: "TargetPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_TransactionId",
                table: "CreditTransactions",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCredits_MembershipNumber",
                table: "MemberCredits",
                column: "MembershipNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "MemberCredits");

            migrationBuilder.DropColumn(
                name: "CreditApplied",
                table: "Payments");
        }
    }
}

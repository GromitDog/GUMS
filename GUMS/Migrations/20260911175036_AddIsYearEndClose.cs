using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddIsYearEndClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsYearEndClose",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Closing journals posted before this flag existed are recognised by the description
            // FinaliseYearEndAsync has always used, so they stop cancelling the closed year's figures.
            migrationBuilder.Sql("UPDATE Transactions SET IsYearEndClose = 1 WHERE Description LIKE 'Year end close – %'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsYearEndClose",
                table: "Transactions");
        }
    }
}

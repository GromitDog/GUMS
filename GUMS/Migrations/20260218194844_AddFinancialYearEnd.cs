using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialYearEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinancialYearEndDay",
                table: "UnitConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 31);

            migrationBuilder.AddColumn<int>(
                name: "FinancialYearEndMonth",
                table: "UnitConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinancialYearEndDay",
                table: "UnitConfigurations");

            migrationBuilder.DropColumn(
                name: "FinancialYearEndMonth",
                table: "UnitConfigurations");
        }
    }
}

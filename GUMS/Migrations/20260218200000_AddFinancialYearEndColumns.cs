using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialYearEndColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE UnitConfigurations ADD COLUMN FinancialYearEndDay INTEGER NOT NULL DEFAULT 31");
            migrationBuilder.Sql("ALTER TABLE UnitConfigurations ADD COLUMN FinancialYearEndMonth INTEGER NOT NULL DEFAULT 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

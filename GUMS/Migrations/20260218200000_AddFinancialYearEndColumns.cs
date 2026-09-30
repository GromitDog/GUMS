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
            // Intentionally empty: AddFinancialYearEnd already adds FinancialYearEndDay and
            // FinancialYearEndMonth, so adding them again here broke every fresh database with
            // "duplicate column name". Databases that got the columns from this migration's
            // original raw SQL have it recorded in __EFMigrationsHistory and never re-run it.
            // Do not delete or rename it; those databases reference it by MigrationId.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

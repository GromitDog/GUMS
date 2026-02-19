using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GUMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsLockedUntil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE UnitConfigurations ADD COLUMN AccountsLockedUntil TEXT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite does not support dropping columns; migration is intentionally left empty.
        }
    }
}

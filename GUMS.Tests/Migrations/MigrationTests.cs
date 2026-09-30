using FluentAssertions;
using GUMS.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GUMS.Tests.Migrations;

/// <summary>
/// Runs the real migrations against SQLite. The in-memory provider used by the service
/// tests never runs migrations, so it cannot catch a migration that breaks startup.
/// </summary>
public class MigrationTests : IDisposable
{
    private const string AddJoiningFee = "20260215180224_AddJoiningFee";
    private const string AddFinancialYearEnd = "20260218194844_AddFinancialYearEnd";
    private const string AddFinancialYearEndColumns = "20260218200000_AddFinancialYearEndColumns";

    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;

    public MigrationTests()
    {
        // Arrange - a private in-memory SQLite database that lives as long as the connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Migrate_ShouldApplyEveryMigration_WhenDatabaseIsNew()
    {
        // Act
        _context.Database.Migrate();

        // Assert
        _context.Database.GetPendingMigrations().Should().BeEmpty();
        GetColumnNames("UnitConfigurations")
            .Should().Contain(new[] { "FinancialYearEndDay", "FinancialYearEndMonth" });
    }

    [Fact]
    public void Migrate_ShouldKeepFinancialYearEnd_WhenBothFinancialYearEndMigrationsAlreadyApplied()
    {
        // Arrange - the owner's live database: AddFinancialYearEnd was recorded without adding
        // any columns, then AddFinancialYearEndColumns added them with raw SQL
        _context.GetService<IMigrator>().Migrate(AddJoiningFee);
        _context.Database.ExecuteSqlRaw(
            "ALTER TABLE UnitConfigurations ADD COLUMN FinancialYearEndDay INTEGER NOT NULL DEFAULT 31");
        _context.Database.ExecuteSqlRaw(
            "ALTER TABLE UnitConfigurations ADD COLUMN FinancialYearEndMonth INTEGER NOT NULL DEFAULT 7");
        RecordAsApplied(AddFinancialYearEnd);
        RecordAsApplied(AddFinancialYearEndColumns);

        // The unit has moved its financial year end to 31 March
        _context.Database.ExecuteSqlRaw(
            """
            INSERT INTO UnitConfigurations (UnitName, UnitType, MeetingDayOfWeek,
                DefaultMeetingStartTime, DefaultMeetingEndTime, DefaultLocationName,
                DefaultSubsAmount, PaymentTermDays, FinancialYearEndDay, FinancialYearEndMonth)
            VALUES ('1st Test Unit', 0, 2, '18:00:00', '19:30:00', 'Village Hall', '20.0', 14, 31, 3)
            """);

        // Act
        _context.Database.Migrate();

        // Assert
        _context.Database.GetPendingMigrations().Should().BeEmpty();
        var config = _context.UnitConfigurations.AsNoTracking().Single();
        config.FinancialYearEndDay.Should().Be(31);
        config.FinancialYearEndMonth.Should().Be(3);
    }

    private void RecordAsApplied(string migrationId) =>
        _context.Database.ExecuteSql(
            $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({migrationId}, '9.0.0')");

    private List<string> GetColumnNames(string table) =>
        _context.Database
            .SqlQuery<string>($"SELECT name AS Value FROM pragma_table_info({table})")
            .ToList();
}

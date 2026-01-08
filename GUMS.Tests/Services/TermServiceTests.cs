using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Tests.Services;

public class TermServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TermService _sut; // System Under Test

    public TermServiceTests()
    {
        // Arrange - Create in-memory database for each test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _sut = new TermService(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoTermsExist()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllTerms_OrderedByStartDateDescending()
    {
        // Arrange
        var term1 = new Term { Name = "Spring 2025", StartDate = new DateTime(2025, 1, 1), EndDate = new DateTime(2025, 4, 30), SubsAmount = 20 };
        var term2 = new Term { Name = "Autumn 2025", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2025, 12, 31), SubsAmount = 20 };
        var term3 = new Term { Name = "Summer 2025", StartDate = new DateTime(2025, 5, 1), EndDate = new DateTime(2025, 8, 31), SubsAmount = 15 };

        _context.Terms.AddRange(term1, term2, term3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Autumn 2025"); // Most recent start date first
        result[1].Name.Should().Be("Summer 2025");
        result[2].Name.Should().Be("Spring 2025");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTerm_WhenExists()
    {
        // Arrange
        var term = new Term { Name = "Test Term", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(90), SubsAmount = 20 };
        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(term.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Term");
        result.SubsAmount.Should().Be(20);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _sut.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetCurrentTermAsync Tests

    [Fact]
    public async Task GetCurrentTermAsync_ShouldReturnCurrentTerm_WhenTodayIsWithinRange()
    {
        // Arrange
        var today = DateTime.Today;
        var pastTerm = new Term { Name = "Past", StartDate = today.AddDays(-100), EndDate = today.AddDays(-10), SubsAmount = 20 };
        var currentTerm = new Term { Name = "Current", StartDate = today.AddDays(-10), EndDate = today.AddDays(80), SubsAmount = 20 };
        var futureTerm = new Term { Name = "Future", StartDate = today.AddDays(90), EndDate = today.AddDays(180), SubsAmount = 20 };

        _context.Terms.AddRange(pastTerm, currentTerm, futureTerm);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetCurrentTermAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Current");
    }

    [Fact]
    public async Task GetCurrentTermAsync_ShouldReturnNull_WhenNoCurrentTerm()
    {
        // Arrange
        var today = DateTime.Today;
        var pastTerm = new Term { Name = "Past", StartDate = today.AddDays(-100), EndDate = today.AddDays(-10), SubsAmount = 20 };
        var futureTerm = new Term { Name = "Future", StartDate = today.AddDays(10), EndDate = today.AddDays(100), SubsAmount = 20 };

        _context.Terms.AddRange(pastTerm, futureTerm);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetCurrentTermAsync();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetFutureTermsAsync Tests

    [Fact]
    public async Task GetFutureTermsAsync_ShouldReturnOnlyFutureTerms_OrderedByStartDate()
    {
        // Arrange
        var today = DateTime.Today;
        var pastTerm = new Term { Name = "Past", StartDate = today.AddDays(-100), EndDate = today.AddDays(-10), SubsAmount = 20 };
        var currentTerm = new Term { Name = "Current", StartDate = today.AddDays(-10), EndDate = today.AddDays(10), SubsAmount = 20 };
        var future1 = new Term { Name = "Future 1", StartDate = today.AddDays(20), EndDate = today.AddDays(110), SubsAmount = 20 };
        var future2 = new Term { Name = "Future 2", StartDate = today.AddDays(120), EndDate = today.AddDays(210), SubsAmount = 20 };

        _context.Terms.AddRange(pastTerm, currentTerm, future1, future2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetFutureTermsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Future 1"); // Earliest future term first
        result[1].Name.Should().Be("Future 2");
    }

    #endregion

    #region GetPastTermsAsync Tests

    [Fact]
    public async Task GetPastTermsAsync_ShouldReturnOnlyPastTerms_OrderedByEndDateDescending()
    {
        // Arrange
        var today = DateTime.Today;
        var past1 = new Term { Name = "Past 1", StartDate = today.AddDays(-200), EndDate = today.AddDays(-110), SubsAmount = 20 };
        var past2 = new Term { Name = "Past 2", StartDate = today.AddDays(-100), EndDate = today.AddDays(-10), SubsAmount = 20 };
        var currentTerm = new Term { Name = "Current", StartDate = today.AddDays(-5), EndDate = today.AddDays(85), SubsAmount = 20 };
        var futureTerm = new Term { Name = "Future", StartDate = today.AddDays(100), EndDate = today.AddDays(190), SubsAmount = 20 };

        _context.Terms.AddRange(past1, past2, currentTerm, futureTerm);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetPastTermsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Past 2"); // Most recently ended first
        result[1].Name.Should().Be("Past 1");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateTerm_WhenValid()
    {
        // Arrange
        var term = new Term
        {
            Name = "Autumn 2026",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 20),
            SubsAmount = 25
        };

        // Act
        var result = await _sut.CreateAsync(term);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();

        var savedTerm = await _context.Terms.FirstOrDefaultAsync(t => t.Name == "Autumn 2026");
        savedTerm.Should().NotBeNull();
        savedTerm!.SubsAmount.Should().Be(25);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenEndDateBeforeStartDate()
    {
        // Arrange
        var term = new Term
        {
            Name = "Invalid Term",
            StartDate = new DateTime(2026, 12, 1),
            EndDate = new DateTime(2026, 9, 1), // End before start
            SubsAmount = 20
        };

        // Act
        var result = await _sut.CreateAsync(term);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("End date must be after start date");

        var savedTerm = await _context.Terms.FirstOrDefaultAsync(t => t.Name == "Invalid Term");
        savedTerm.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenSubsAmountNegative()
    {
        // Arrange
        var term = new Term
        {
            Name = "Invalid Subs",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 1),
            SubsAmount = -10 // Negative amount
        };

        // Act
        var result = await _sut.CreateAsync(term);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenOverlapsExistingTerm()
    {
        // Arrange
        var existingTerm = new Term
        {
            Name = "Existing",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(existingTerm);
        await _context.SaveChangesAsync();

        var overlappingTerm = new Term
        {
            Name = "Overlapping",
            StartDate = new DateTime(2026, 10, 1), // Starts within existing term
            EndDate = new DateTime(2027, 2, 28),
            SubsAmount = 20
        };

        // Act
        var result = await _sut.CreateAsync(overlappingTerm);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("overlaps");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTerm_WhenValid()
    {
        // Arrange
        var term = new Term
        {
            Name = "Original",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        // Modify the term
        term.Name = "Updated";
        term.SubsAmount = 25;

        // Act
        var result = await _sut.UpdateAsync(term);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTerm = await _context.Terms.FindAsync(term.Id);
        updatedTerm!.Name.Should().Be("Updated");
        updatedTerm.SubsAmount.Should().Be(25);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenTermNotFound()
    {
        // Arrange
        var nonExistentTerm = new Term
        {
            Id = 999,
            Name = "Non-existent",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(90),
            SubsAmount = 20
        };

        // Act
        var result = await _sut.UpdateAsync(nonExistentTerm);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateAsync_ShouldAllowSameDateRange_ForSameTerm()
    {
        // Arrange
        var term = new Term
        {
            Name = "Test Term",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        // Update without changing dates (should not fail overlap check)
        term.Name = "Updated Name";
        term.SubsAmount = 25;

        // Act
        var result = await _sut.UpdateAsync(term);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteTerm_WhenNoMeetingsOrPayments()
    {
        // Arrange
        var term = new Term
        {
            Name = "To Delete",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(term.Id);

        // Assert
        result.Success.Should().BeTrue();

        var deletedTerm = await _context.Terms.FindAsync(term.Id);
        deletedTerm.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenTermNotFound()
    {
        // Act
        var result = await _sut.DeleteAsync(999);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenMeetingsExistInDateRange()
    {
        // Arrange
        var term = new Term
        {
            Name = "Term with Meeting",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        // Add a meeting within the term's date range
        var meeting = new Meeting
        {
            Date = new DateTime(2026, 10, 15), // Within term range
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = Data.Enums.MeetingType.Regular,
            Title = "Test Meeting",
            LocationName = "Hall"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(term.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("meetings exist");
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenPaymentsLinkedToTerm()
    {
        // Arrange
        var term = new Term
        {
            Name = "Term with Payments",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        // Add a payment linked to this term
        var payment = new Payment
        {
            MembershipNumber = "12345",
            Amount = 20,
            AmountPaid = 0,
            PaymentType = Data.Enums.PaymentType.Subs,
            Status = Data.Enums.PaymentStatus.Pending,
            DueDate = new DateTime(2026, 9, 15),
            TermId = term.Id
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(term.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("payments are linked");
    }

    #endregion

    #region ValidateNoOverlapAsync Tests

    [Fact]
    public async Task ValidateNoOverlapAsync_ShouldReturnTrue_WhenNoOverlap()
    {
        // Arrange
        var existingTerm = new Term
        {
            Name = "Existing",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 4, 30),
            SubsAmount = 20
        };
        _context.Terms.Add(existingTerm);
        await _context.SaveChangesAsync();

        var newTerm = new Term
        {
            Name = "New",
            StartDate = new DateTime(2026, 9, 1), // No overlap
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };

        // Act
        var result = await _sut.ValidateNoOverlapAsync(newTerm);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateNoOverlapAsync_ShouldReturnFalse_WhenStartDateOverlaps()
    {
        // Arrange
        var existingTerm = new Term
        {
            Name = "Existing",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(existingTerm);
        await _context.SaveChangesAsync();

        var newTerm = new Term
        {
            Name = "New",
            StartDate = new DateTime(2026, 10, 1), // Starts within existing
            EndDate = new DateTime(2027, 2, 28),
            SubsAmount = 20
        };

        // Act
        var result = await _sut.ValidateNoOverlapAsync(newTerm);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateNoOverlapAsync_ShouldReturnFalse_WhenEndDateOverlaps()
    {
        // Arrange
        var existingTerm = new Term
        {
            Name = "Existing",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(existingTerm);
        await _context.SaveChangesAsync();

        var newTerm = new Term
        {
            Name = "New",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 10, 15), // Ends within existing
            SubsAmount = 20
        };

        // Act
        var result = await _sut.ValidateNoOverlapAsync(newTerm);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateNoOverlapAsync_ShouldReturnFalse_WhenCompletelyContainsExisting()
    {
        // Arrange
        var existingTerm = new Term
        {
            Name = "Existing",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 11, 30),
            SubsAmount = 20
        };
        _context.Terms.Add(existingTerm);
        await _context.SaveChangesAsync();

        var newTerm = new Term
        {
            Name = "New",
            StartDate = new DateTime(2026, 9, 1), // Completely contains existing
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };

        // Act
        var result = await _sut.ValidateNoOverlapAsync(newTerm);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateNoOverlapAsync_ShouldExcludeSpecifiedTerm_WhenUpdating()
    {
        // Arrange
        var term = new Term
        {
            Name = "Test Term",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 12, 31),
            SubsAmount = 20
        };
        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        // Act - Validate same term with excludeTermId
        var result = await _sut.ValidateNoOverlapAsync(term, term.Id);

        // Assert
        result.Should().BeTrue(); // Should not detect itself as overlapping
    }

    #endregion
}

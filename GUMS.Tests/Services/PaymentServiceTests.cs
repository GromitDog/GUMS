using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GUMS.Tests.Services;

public class PaymentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITermService> _mockTermService;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly PaymentService _sut; // System Under Test

    public PaymentServiceTests()
    {
        // Arrange - Create in-memory database for each test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        // Mock services
        _mockTermService = new Mock<ITermService>();
        _mockConfigService = new Mock<IConfigurationService>();

        // Setup default configuration
        _mockConfigService.Setup(x => x.GetConfigurationAsync())
            .ReturnsAsync(new UnitConfiguration
            {
                UnitName = "Test Unit",
                UnitType = Section.Brownie,
                MeetingDayOfWeek = DayOfWeek.Wednesday,
                DefaultMeetingStartTime = new TimeOnly(18, 30),
                DefaultMeetingEndTime = new TimeOnly(20, 0),
                DefaultLocationName = "Village Hall",
                DefaultSubsAmount = 25.00m,
                PaymentTermDays = 14
            });

        _sut = new PaymentService(_context, _mockTermService.Object, _mockConfigService.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Helper Methods

    private async Task<Term> CreateTestTermAsync(DateTime? startDate = null, DateTime? endDate = null, decimal subsAmount = 25.00m)
    {
        var term = new Term
        {
            Name = "Spring 2026",
            StartDate = startDate ?? new DateTime(2026, 1, 6),
            EndDate = endDate ?? new DateTime(2026, 4, 3),
            SubsAmount = subsAmount
        };

        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        _mockTermService.Setup(x => x.GetByIdAsync(term.Id))
            .ReturnsAsync(term);
        _mockTermService.Setup(x => x.GetCurrentTermAsync())
            .ReturnsAsync(term);

        return term;
    }

    private async Task<Person> CreateTestPersonAsync(string membershipNumber, PersonType type = PersonType.Girl, bool isActive = true)
    {
        var person = new Person
        {
            MembershipNumber = membershipNumber,
            FullName = $"Test Person {membershipNumber}",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = type,
            Section = type == PersonType.Girl ? Section.Brownie : null,
            DateJoined = new DateTime(2024, 1, 1),
            IsActive = isActive
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();
        return person;
    }

    private async Task<Meeting> CreateTestMeetingAsync(DateTime? date = null, decimal? costPerAttendee = null)
    {
        var meeting = new Meeting
        {
            Date = date ?? DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Extra,
            Title = "Test Meeting",
            LocationName = "Village Hall",
            CostPerAttendee = costPerAttendee,
            PaymentDeadline = costPerAttendee.HasValue ? date?.AddDays(-7) ?? DateTime.Today.AddDays(-7) : null
        };

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        return meeting;
    }

    private async Task<Payment> CreateTestPaymentAsync(
        string membershipNumber,
        decimal amount = 25.00m,
        PaymentType type = PaymentType.Subs,
        PaymentStatus status = PaymentStatus.Pending,
        DateTime? dueDate = null,
        int? termId = null,
        int? meetingId = null)
    {
        var payment = new Payment
        {
            MembershipNumber = membershipNumber,
            Amount = amount,
            PaymentType = type,
            Status = status,
            DueDate = dueDate ?? DateTime.Today.AddDays(14),
            Reference = $"Test Payment - {membershipNumber}",
            TermId = termId,
            MeetingId = meetingId
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenPaymentDoesNotExist()
    {
        // Act
        var result = await _sut.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPayment_WhenExists()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");

        // Act
        var result = await _sut.GetByIdAsync(payment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.MembershipNumber.Should().Be("M001");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldIncludeTermNavigation()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        var payment = await CreateTestPaymentAsync("M001", termId: term.Id);

        // Act
        var result = await _sut.GetByIdAsync(payment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Term.Should().NotBeNull();
        result.Term!.Name.Should().Be("Spring 2026");
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoPayments()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllPayments()
    {
        // Arrange
        await CreateTestPaymentAsync("M001");
        await CreateTestPaymentAsync("M002");
        await CreateTestPaymentAsync("M003");

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOrderedByDueDateDescending()
    {
        // Arrange
        await CreateTestPaymentAsync("M001", dueDate: new DateTime(2026, 2, 1));
        await CreateTestPaymentAsync("M002", dueDate: new DateTime(2026, 3, 1));
        await CreateTestPaymentAsync("M003", dueDate: new DateTime(2026, 1, 1));

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result[0].DueDate.Should().Be(new DateTime(2026, 3, 1));
        result[1].DueDate.Should().Be(new DateTime(2026, 2, 1));
        result[2].DueDate.Should().Be(new DateTime(2026, 1, 1));
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreatePayment()
    {
        // Arrange
        var payment = new Payment
        {
            MembershipNumber = "M001",
            Amount = 25.00m,
            PaymentType = PaymentType.Subs,
            DueDate = DateTime.Today.AddDays(14),
            Reference = "Test Payment"
        };

        // Act
        var result = await _sut.CreateAsync(payment);

        // Assert
        result.Success.Should().BeTrue();
        result.Payment.Should().NotBeNull();
        result.Payment!.Id.Should().BeGreaterThan(0);

        var saved = await _context.Payments.FirstOrDefaultAsync(p => p.MembershipNumber == "M001");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenMembershipNumberEmpty()
    {
        // Arrange
        var payment = new Payment
        {
            MembershipNumber = "",
            Amount = 25.00m,
            PaymentType = PaymentType.Subs,
            DueDate = DateTime.Today.AddDays(14),
            Reference = "Test"
        };

        // Act
        var result = await _sut.CreateAsync(payment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Membership number");
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenAmountZero()
    {
        // Arrange
        var payment = new Payment
        {
            MembershipNumber = "M001",
            Amount = 0,
            PaymentType = PaymentType.Subs,
            DueDate = DateTime.Today.AddDays(14),
            Reference = "Test"
        };

        // Act
        var result = await _sut.CreateAsync(payment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Amount");
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenReferenceEmpty()
    {
        // Arrange
        var payment = new Payment
        {
            MembershipNumber = "M001",
            Amount = 25.00m,
            PaymentType = PaymentType.Subs,
            DueDate = DateTime.Today.AddDays(14),
            Reference = ""
        };

        // Act
        var result = await _sut.CreateAsync(payment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Reference");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdatePayment()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", amount: 25.00m);

        payment.Amount = 30.00m;
        payment.Reference = "Updated Reference";

        // Act
        var result = await _sut.UpdateAsync(payment);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Payments.FindAsync(payment.Id);
        updated!.Amount.Should().Be(30.00m);
        updated.Reference.Should().Be("Updated Reference");
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenPaymentNotFound()
    {
        // Arrange
        var payment = new Payment { Id = 999 };

        // Act
        var result = await _sut.UpdateAsync(payment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenPaymentAlreadyPaid()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", status: PaymentStatus.Paid);
        payment.Amount = 30.00m;

        // Act
        var result = await _sut.UpdateAsync(payment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fully paid");
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenPaymentCancelled()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", status: PaymentStatus.Cancelled);
        payment.Amount = 30.00m;

        // Act
        var result = await _sut.UpdateAsync(payment);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeletePayment()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");

        // Act
        var result = await _sut.DeleteAsync(payment.Id);

        // Assert
        result.Success.Should().BeTrue();

        var deleted = await _context.Payments.FindAsync(payment.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenPaymentNotFound()
    {
        // Act
        var result = await _sut.DeleteAsync(999);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenPaymentHasBeenPaid()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");
        payment.AmountPaid = 10.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(payment.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Cancel it instead");
    }

    #endregion

    #region GetByStatusAsync Tests

    [Fact]
    public async Task GetByStatusAsync_ShouldReturnOnlyMatchingStatus()
    {
        // Arrange
        await CreateTestPaymentAsync("M001", status: PaymentStatus.Pending);
        await CreateTestPaymentAsync("M002", status: PaymentStatus.Paid);
        await CreateTestPaymentAsync("M003", status: PaymentStatus.Pending);
        await CreateTestPaymentAsync("M004", status: PaymentStatus.Cancelled);

        // Act
        var result = await _sut.GetByStatusAsync(PaymentStatus.Pending);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.Status == PaymentStatus.Pending);
    }

    #endregion

    #region GetByMembershipNumberAsync Tests

    [Fact]
    public async Task GetByMembershipNumberAsync_ShouldReturnOnlyMatchingMember()
    {
        // Arrange
        await CreateTestPaymentAsync("M001");
        await CreateTestPaymentAsync("M001");
        await CreateTestPaymentAsync("M002");

        // Act
        var result = await _sut.GetByMembershipNumberAsync("M001");

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.MembershipNumber == "M001");
    }

    #endregion

    #region GetByTermAsync Tests

    [Fact]
    public async Task GetByTermAsync_ShouldReturnOnlyMatchingTerm()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        await CreateTestPaymentAsync("M001", termId: term.Id);
        await CreateTestPaymentAsync("M002", termId: term.Id);
        await CreateTestPaymentAsync("M003"); // No term

        // Act
        var result = await _sut.GetByTermAsync(term.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.TermId == term.Id);
    }

    #endregion

    #region GetByMeetingAsync Tests

    [Fact]
    public async Task GetByMeetingAsync_ShouldReturnOnlyMatchingMeeting()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync(costPerAttendee: 15.00m);
        await CreateTestPaymentAsync("M001", type: PaymentType.Activity, meetingId: meeting.Id);
        await CreateTestPaymentAsync("M002", type: PaymentType.Activity, meetingId: meeting.Id);
        await CreateTestPaymentAsync("M003"); // No meeting

        // Act
        var result = await _sut.GetByMeetingAsync(meeting.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.MeetingId == meeting.Id);
    }

    #endregion

    #region GetOverduePaymentsAsync Tests

    [Fact]
    public async Task GetOverduePaymentsAsync_ShouldReturnOnlyOverdue()
    {
        // Arrange
        await CreateTestPaymentAsync("M001", dueDate: DateTime.Today.AddDays(-10)); // Overdue
        await CreateTestPaymentAsync("M002", dueDate: DateTime.Today.AddDays(-5));  // Overdue
        await CreateTestPaymentAsync("M003", dueDate: DateTime.Today.AddDays(10));  // Not due yet
        await CreateTestPaymentAsync("M004", dueDate: DateTime.Today.AddDays(-5), status: PaymentStatus.Paid); // Paid

        // Act
        var result = await _sut.GetOverduePaymentsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.Status == PaymentStatus.Pending && p.DueDate < DateTime.Today);
    }

    [Fact]
    public async Task GetOverduePaymentsAsync_ShouldOrderByDueDateAscending()
    {
        // Arrange
        await CreateTestPaymentAsync("M001", dueDate: DateTime.Today.AddDays(-5));
        await CreateTestPaymentAsync("M002", dueDate: DateTime.Today.AddDays(-10));
        await CreateTestPaymentAsync("M003", dueDate: DateTime.Today.AddDays(-3));

        // Act
        var result = await _sut.GetOverduePaymentsAsync();

        // Assert
        result[0].DueDate.Should().Be(DateTime.Today.AddDays(-10)); // Oldest first
        result[1].DueDate.Should().Be(DateTime.Today.AddDays(-5));
        result[2].DueDate.Should().Be(DateTime.Today.AddDays(-3));
    }

    #endregion

    #region GetPendingPaymentsForMemberAsync Tests

    [Fact]
    public async Task GetPendingPaymentsForMemberAsync_ShouldReturnOnlyPendingForMember()
    {
        // Arrange
        await CreateTestPaymentAsync("M001", status: PaymentStatus.Pending);
        await CreateTestPaymentAsync("M001", status: PaymentStatus.Paid);
        await CreateTestPaymentAsync("M002", status: PaymentStatus.Pending);

        // Act
        var result = await _sut.GetPendingPaymentsForMemberAsync("M001");

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be(PaymentStatus.Pending);
    }

    #endregion

    #region GenerateTermlySubsAsync Tests

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldCreatePaymentsForActiveGirls()
    {
        // Arrange
        var term = await CreateTestTermAsync(subsAmount: 30.00m);
        await CreateTestPersonAsync("M001", PersonType.Girl, isActive: true);
        await CreateTestPersonAsync("M002", PersonType.Girl, isActive: true);
        await CreateTestPersonAsync("L001", PersonType.Leader, isActive: true); // Leader - should be skipped

        // Act
        var result = await _sut.GenerateTermlySubsAsync(term.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.PaymentsCreated.Should().Be(2);

        var payments = await _context.Payments.Where(p => p.TermId == term.Id).ToListAsync();
        payments.Should().HaveCount(2);
        payments.Should().OnlyContain(p => p.Amount == 30.00m && p.PaymentType == PaymentType.Subs);
    }

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldSkipInactiveGirls()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        await CreateTestPersonAsync("M001", PersonType.Girl, isActive: true);
        await CreateTestPersonAsync("M002", PersonType.Girl, isActive: false); // Inactive

        // Act
        var result = await _sut.GenerateTermlySubsAsync(term.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.PaymentsCreated.Should().Be(1);
    }

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldSkipExistingPayments()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        await CreateTestPersonAsync("M001", PersonType.Girl, isActive: true);
        await CreateTestPersonAsync("M002", PersonType.Girl, isActive: true);

        // Create existing payment for M001
        await CreateTestPaymentAsync("M001", type: PaymentType.Subs, termId: term.Id);

        // Act
        var result = await _sut.GenerateTermlySubsAsync(term.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.PaymentsCreated.Should().Be(1); // Only M002

        var payments = await _context.Payments.Where(p => p.TermId == term.Id).ToListAsync();
        payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldFail_WhenTermNotFound()
    {
        // Act
        var result = await _sut.GenerateTermlySubsAsync(999);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Term not found");
    }

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldFail_WhenSubsAmountZero()
    {
        // Arrange
        var term = await CreateTestTermAsync(subsAmount: 0);

        // Act
        var result = await _sut.GenerateTermlySubsAsync(term.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldFail_WhenNoActiveGirls()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        // No persons created

        // Act
        var result = await _sut.GenerateTermlySubsAsync(term.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active girls");
    }

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldUseDueDateFromConfig()
    {
        // Arrange
        var term = await CreateTestTermAsync(startDate: new DateTime(2026, 1, 6));
        await CreateTestPersonAsync("M001", PersonType.Girl);

        // PaymentTermDays = 14 from config

        // Act
        var result = await _sut.GenerateTermlySubsAsync(term.Id);

        // Assert
        result.Success.Should().BeTrue();

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TermId == term.Id);
        payment!.DueDate.Should().Be(new DateTime(2026, 1, 20)); // StartDate + 14 days
    }

    [Fact]
    public async Task GenerateTermlySubsAsync_ShouldUseTermSubsAmount()
    {
        // Arrange
        var term = await CreateTestTermAsync(subsAmount: 35.00m);
        await CreateTestPersonAsync("M001", PersonType.Girl);

        // Act
        var result = await _sut.GenerateTermlySubsAsync(term.Id);

        // Assert
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TermId == term.Id);
        payment!.Amount.Should().Be(35.00m);
    }

    #endregion

    #region HasTermlySubsBeenGeneratedAsync Tests

    [Fact]
    public async Task HasTermlySubsBeenGeneratedAsync_ShouldReturnFalse_WhenNoSubsExist()
    {
        // Arrange
        var term = await CreateTestTermAsync();

        // Act
        var result = await _sut.HasTermlySubsBeenGeneratedAsync(term.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasTermlySubsBeenGeneratedAsync_ShouldReturnTrue_WhenSubsExist()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        await CreateTestPaymentAsync("M001", type: PaymentType.Subs, termId: term.Id);

        // Act
        var result = await _sut.HasTermlySubsBeenGeneratedAsync(term.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasTermlySubsBeenGeneratedAsync_ShouldReturnFalse_WhenOnlyActivityPaymentsExist()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        await CreateTestPaymentAsync("M001", type: PaymentType.Activity, termId: term.Id);

        // Act
        var result = await _sut.HasTermlySubsBeenGeneratedAsync(term.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetEligibleMembersCountForTermAsync Tests

    [Fact]
    public async Task GetEligibleMembersCountForTermAsync_ShouldCountActiveGirlsWithoutPayments()
    {
        // Arrange
        var term = await CreateTestTermAsync();
        await CreateTestPersonAsync("M001", PersonType.Girl, isActive: true);
        await CreateTestPersonAsync("M002", PersonType.Girl, isActive: true);
        await CreateTestPersonAsync("M003", PersonType.Girl, isActive: true);

        // M001 already has payment
        await CreateTestPaymentAsync("M001", type: PaymentType.Subs, termId: term.Id);

        // Act
        var result = await _sut.GetEligibleMembersCountForTermAsync(term.Id);

        // Assert
        result.Should().Be(2);
    }

    #endregion

    #region CreateActivityPaymentAsync Tests

    [Fact]
    public async Task CreateActivityPaymentAsync_ShouldCreatePayment()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync(date: new DateTime(2026, 3, 15), costPerAttendee: 15.00m);
        await CreateTestPersonAsync("M001");

        // Act
        var result = await _sut.CreateActivityPaymentAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeTrue();
        result.Payment.Should().NotBeNull();
        result.Payment!.Amount.Should().Be(15.00m);
        result.Payment.PaymentType.Should().Be(PaymentType.Activity);
        result.Payment.MeetingId.Should().Be(meeting.Id);
    }

    [Fact]
    public async Task CreateActivityPaymentAsync_ShouldFail_WhenMeetingNotFound()
    {
        // Act
        var result = await _sut.CreateActivityPaymentAsync(999, "M001");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Meeting not found");
    }

    [Fact]
    public async Task CreateActivityPaymentAsync_ShouldFail_WhenMeetingHasNoCost()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync(costPerAttendee: null);

        // Act
        var result = await _sut.CreateActivityPaymentAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no cost");
    }

    [Fact]
    public async Task CreateActivityPaymentAsync_ShouldFail_WhenPaymentAlreadyExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync(costPerAttendee: 15.00m);
        await CreateTestPaymentAsync("M001", type: PaymentType.Activity, meetingId: meeting.Id);

        // Act
        var result = await _sut.CreateActivityPaymentAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateActivityPaymentAsync_ShouldUsePaymentDeadlineAsDueDate()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync(date: new DateTime(2026, 3, 15), costPerAttendee: 15.00m);
        meeting.PaymentDeadline = new DateTime(2026, 3, 8);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CreateActivityPaymentAsync(meeting.Id, "M001");

        // Assert
        result.Payment!.DueDate.Should().Be(new DateTime(2026, 3, 8));
    }

    #endregion

    #region HasActivityPaymentAsync Tests

    [Fact]
    public async Task HasActivityPaymentAsync_ShouldReturnFalse_WhenNoPaymentExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync(costPerAttendee: 15.00m);

        // Act
        var result = await _sut.HasActivityPaymentAsync(meeting.Id, "M001");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActivityPaymentAsync_ShouldReturnTrue_WhenPaymentExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync(costPerAttendee: 15.00m);
        await CreateTestPaymentAsync("M001", type: PaymentType.Activity, meetingId: meeting.Id);

        // Act
        var result = await _sut.HasActivityPaymentAsync(meeting.Id, "M001");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region RecordPaymentAsync Tests

    [Fact]
    public async Task RecordPaymentAsync_ShouldRecordFullPayment()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", amount: 25.00m);

        // Act
        var result = await _sut.RecordPaymentAsync(payment.Id, 25.00m, PaymentMethod.Cash);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Payments.FindAsync(payment.Id);
        updated!.AmountPaid.Should().Be(25.00m);
        updated.Status.Should().Be(PaymentStatus.Paid);
        updated.PaymentMethod.Should().Be(PaymentMethod.Cash);
        updated.PaymentDate.Should().Be(DateTime.Today);
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldRecordPartialPayment()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", amount: 25.00m);

        // Act
        var result = await _sut.RecordPaymentAsync(payment.Id, 15.00m, PaymentMethod.BankTransfer);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Payments.FindAsync(payment.Id);
        updated!.AmountPaid.Should().Be(15.00m);
        updated.OutstandingBalance.Should().Be(10.00m);
        updated.Status.Should().Be(PaymentStatus.Pending); // Still pending
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldAccumulateMultiplePartialPayments()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", amount: 25.00m);

        // Act
        await _sut.RecordPaymentAsync(payment.Id, 10.00m, PaymentMethod.Cash);
        await _sut.RecordPaymentAsync(payment.Id, 10.00m, PaymentMethod.Cash);
        var result = await _sut.RecordPaymentAsync(payment.Id, 5.00m, PaymentMethod.Cash);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Payments.FindAsync(payment.Id);
        updated!.AmountPaid.Should().Be(25.00m);
        updated.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldFail_WhenPaymentNotFound()
    {
        // Act
        var result = await _sut.RecordPaymentAsync(999, 25.00m, PaymentMethod.Cash);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldFail_WhenPaymentCancelled()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", status: PaymentStatus.Cancelled);

        // Act
        var result = await _sut.RecordPaymentAsync(payment.Id, 25.00m, PaymentMethod.Cash);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldFail_WhenPaymentAlreadyPaid()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", status: PaymentStatus.Paid);

        // Act
        var result = await _sut.RecordPaymentAsync(payment.Id, 25.00m, PaymentMethod.Cash);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fully paid");
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldFail_WhenAmountZero()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");

        // Act
        var result = await _sut.RecordPaymentAsync(payment.Id, 0, PaymentMethod.Cash);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldFail_WhenAmountExceedsOutstanding()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", amount: 25.00m);

        // Act
        var result = await _sut.RecordPaymentAsync(payment.Id, 30.00m, PaymentMethod.Cash);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds outstanding");
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldAppendNotes()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");
        payment.Notes = "Existing note";
        await _context.SaveChangesAsync();

        // Act
        await _sut.RecordPaymentAsync(payment.Id, 25.00m, PaymentMethod.Cash, notes: "Payment received via bank");

        // Assert
        var updated = await _context.Payments.FindAsync(payment.Id);
        updated!.Notes.Should().Contain("Existing note");
        updated.Notes.Should().Contain("Payment received via bank");
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldUseProvidedDate()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");
        var paymentDate = new DateTime(2026, 1, 15);

        // Act
        await _sut.RecordPaymentAsync(payment.Id, 25.00m, PaymentMethod.Cheque, paymentDate);

        // Assert
        var updated = await _context.Payments.FindAsync(payment.Id);
        updated!.PaymentDate.Should().Be(paymentDate);
    }

    #endregion

    #region CancelPaymentAsync Tests

    [Fact]
    public async Task CancelPaymentAsync_ShouldCancelPayment()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");

        // Act
        var result = await _sut.CancelPaymentAsync(payment.Id, "Family moved away");

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Payments.FindAsync(payment.Id);
        updated!.Status.Should().Be(PaymentStatus.Cancelled);
        updated.Notes.Should().Contain("CANCELLED");
        updated.Notes.Should().Contain("Family moved away");
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldFail_WhenPaymentNotFound()
    {
        // Act
        var result = await _sut.CancelPaymentAsync(999, "Reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldFail_WhenAlreadyCancelled()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", status: PaymentStatus.Cancelled);

        // Act
        var result = await _sut.CancelPaymentAsync(payment.Id, "Reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already cancelled");
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldFail_WhenPaymentPaid()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001", status: PaymentStatus.Paid);

        // Act
        var result = await _sut.CancelPaymentAsync(payment.Id, "Reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fully paid");
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldFail_WhenReasonEmpty()
    {
        // Arrange
        var payment = await CreateTestPaymentAsync("M001");

        // Act
        var result = await _sut.CancelPaymentAsync(payment.Id, "");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("reason is required");
    }

    #endregion

    #region GetTermPaymentStatsAsync Tests

    [Fact]
    public async Task GetTermPaymentStatsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var term = await CreateTestTermAsync();

        await CreateTestPaymentAsync("M001", amount: 25.00m, status: PaymentStatus.Pending, termId: term.Id, dueDate: DateTime.Today.AddDays(-5)); // Overdue
        await CreateTestPaymentAsync("M002", amount: 25.00m, status: PaymentStatus.Pending, termId: term.Id, dueDate: DateTime.Today.AddDays(10));
        await CreateTestPaymentAsync("M003", amount: 25.00m, status: PaymentStatus.Paid, termId: term.Id);
        await CreateTestPaymentAsync("M004", amount: 25.00m, status: PaymentStatus.Cancelled, termId: term.Id);

        // Mark M003 as fully paid
        var m003Payment = await _context.Payments.FirstAsync(p => p.MembershipNumber == "M003");
        m003Payment.AmountPaid = 25.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetTermPaymentStatsAsync(term.Id);

        // Assert
        result.TermId.Should().Be(term.Id);
        result.TermName.Should().Be("Spring 2026");
        result.TotalPayments.Should().Be(4);
        result.PendingPayments.Should().Be(2);
        result.PaidPayments.Should().Be(1);
        result.CancelledPayments.Should().Be(1);
        result.OverduePayments.Should().Be(1);
        result.TotalAmount.Should().Be(75.00m); // Excludes cancelled
        result.TotalPaid.Should().Be(25.00m);
        result.TotalOutstanding.Should().Be(50.00m);
    }

    #endregion

    #region GetMemberPaymentSummaryAsync Tests

    [Fact]
    public async Task GetMemberPaymentSummaryAsync_ShouldReturnCorrectSummary()
    {
        // Arrange
        await CreateTestPersonAsync("M001");

        await CreateTestPaymentAsync("M001", amount: 25.00m, status: PaymentStatus.Pending, dueDate: DateTime.Today.AddDays(-5)); // Overdue
        await CreateTestPaymentAsync("M001", amount: 25.00m, status: PaymentStatus.Pending, dueDate: DateTime.Today.AddDays(10));
        await CreateTestPaymentAsync("M001", amount: 25.00m, status: PaymentStatus.Paid);

        // Mark paid payment as fully paid
        var paidPayment = await _context.Payments.FirstAsync(p => p.MembershipNumber == "M001" && p.Status == PaymentStatus.Paid);
        paidPayment.AmountPaid = 25.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetMemberPaymentSummaryAsync("M001");

        // Assert
        result.MembershipNumber.Should().Be("M001");
        result.MemberName.Should().Be("Test Person M001");
        result.TotalPayments.Should().Be(3);
        result.PendingPayments.Should().Be(2);
        result.OverduePayments.Should().Be(1);
        result.TotalOwed.Should().Be(75.00m);
        result.TotalPaid.Should().Be(25.00m);
        result.TotalOutstanding.Should().Be(50.00m);
    }

    #endregion

    #region GetDashboardStatsAsync Tests

    [Fact]
    public async Task GetDashboardStatsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var term = await CreateTestTermAsync(
            startDate: DateTime.Today.AddDays(-30),
            endDate: DateTime.Today.AddDays(60));

        await CreateTestPaymentAsync("M001", status: PaymentStatus.Pending, dueDate: DateTime.Today.AddDays(-5), termId: term.Id); // Overdue
        await CreateTestPaymentAsync("M002", status: PaymentStatus.Pending, dueDate: DateTime.Today.AddDays(10), termId: term.Id);
        await CreateTestPaymentAsync("M003", status: PaymentStatus.Paid, termId: term.Id);

        var paidPayment = await _context.Payments.FirstAsync(p => p.MembershipNumber == "M003");
        paidPayment.AmountPaid = 25.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetDashboardStatsAsync();

        // Assert
        result.PendingCount.Should().Be(2);
        result.OverdueCount.Should().Be(1);
        result.PaidThisTermCount.Should().Be(1);
        result.TotalOutstanding.Should().Be(50.00m);
        result.TotalPaidThisTerm.Should().Be(25.00m);
    }

    #endregion
}

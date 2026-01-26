using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GUMS.Tests.Services;

public class AttendanceServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITermService> _mockTermService;
    private readonly Mock<IMeetingService> _mockMeetingService;
    private readonly AttendanceService _sut; // System Under Test

    public AttendanceServiceTests()
    {
        // Arrange - Create in-memory database for each test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        // Mock term service
        _mockTermService = new Mock<ITermService>();

        // Mock meeting service
        _mockMeetingService = new Mock<IMeetingService>();
        _mockMeetingService.Setup(x => x.CalculateNightsForMeeting(It.IsAny<DateTime>(), It.IsAny<DateTime?>()))
            .Returns((DateTime start, DateTime? end) => end.HasValue ? (end.Value.Date - start.Date).Days : 0);

        _sut = new AttendanceService(_context, _mockTermService.Object, _mockMeetingService.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Helper Methods

    private async Task<Meeting> CreateTestMeetingAsync(DateTime? date = null)
    {
        var meeting = new Meeting
        {
            Date = date ?? DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Test Meeting",
            LocationName = "Village Hall"
        };

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        return meeting;
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

    private async Task<Term> CreateTestTermAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var term = new Term
        {
            Name = "Test Term",
            StartDate = startDate ?? new DateTime(2026, 1, 6),
            EndDate = endDate ?? new DateTime(2026, 4, 3),
            SubsAmount = 20.00m
        };

        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        _mockTermService.Setup(x => x.GetByIdAsync(term.Id))
            .ReturnsAsync(term);

        return term;
    }

    #endregion

    #region GetAttendanceForMeetingAsync Tests

    [Fact]
    public async Task GetAttendanceForMeetingAsync_ShouldReturnEmptyList_WhenNoAttendanceExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        // Act
        var result = await _sut.GetAttendanceForMeetingAsync(meeting.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAttendanceForMeetingAsync_ShouldReturnAllRecords_WhenAttendanceExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M002", Attended = false },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M003", Attended = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAttendanceForMeetingAsync(meeting.Id);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAttendanceForMeetingAsync_ShouldReturnOrderedByMembershipNumber()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M003", Attended = true },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M002", Attended = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAttendanceForMeetingAsync(meeting.Id);

        // Assert
        result[0].MembershipNumber.Should().Be("M001");
        result[1].MembershipNumber.Should().Be("M002");
        result[2].MembershipNumber.Should().Be("M003");
    }

    #endregion

    #region GetAttendanceRecordAsync Tests

    [Fact]
    public async Task GetAttendanceRecordAsync_ShouldReturnNull_WhenRecordDoesNotExist()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        // Act
        var result = await _sut.GetAttendanceRecordAsync(meeting.Id, "M001");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAttendanceRecordAsync_ShouldReturnRecord_WhenExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true,
            Notes = "Test note"
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAttendanceRecordAsync(meeting.Id, "M001");

        // Assert
        result.Should().NotBeNull();
        result!.Attended.Should().BeTrue();
        result.Notes.Should().Be("Test note");
    }

    #endregion

    #region GetAttendanceHistoryForMemberAsync Tests

    [Fact]
    public async Task GetAttendanceHistoryForMemberAsync_ShouldReturnAllRecordsForMember()
    {
        // Arrange
        var meeting1 = await CreateTestMeetingAsync(new DateTime(2026, 1, 1));
        var meeting2 = await CreateTestMeetingAsync(new DateTime(2026, 1, 8));
        var meeting3 = await CreateTestMeetingAsync(new DateTime(2026, 1, 15));

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting2.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting3.Id, MembershipNumber = "M001", Attended = false },
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M002", Attended = true } // Different member
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAttendanceHistoryForMemberAsync("M001");

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(a => a.MembershipNumber == "M001");
    }

    [Fact]
    public async Task GetAttendanceHistoryForMemberAsync_ShouldReturnOrderedByDateDescending()
    {
        // Arrange
        var meeting1 = await CreateTestMeetingAsync(new DateTime(2026, 1, 1));
        var meeting2 = await CreateTestMeetingAsync(new DateTime(2026, 1, 15));
        var meeting3 = await CreateTestMeetingAsync(new DateTime(2026, 1, 8));

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting2.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting3.Id, MembershipNumber = "M001", Attended = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAttendanceHistoryForMemberAsync("M001");

        // Assert
        result[0].Meeting.Date.Should().Be(new DateTime(2026, 1, 15)); // Most recent first
        result[1].Meeting.Date.Should().Be(new DateTime(2026, 1, 8));
        result[2].Meeting.Date.Should().Be(new DateTime(2026, 1, 1));
    }

    #endregion

    #region SaveAttendanceRecordAsync Tests

    [Fact]
    public async Task SaveAttendanceRecordAsync_ShouldCreateNewRecord_WhenNotExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true
        };

        // Act
        var result = await _sut.SaveAttendanceRecordAsync(attendance);

        // Assert
        result.Success.Should().BeTrue();
        result.Attendance.Should().NotBeNull();
        result.Attendance!.Id.Should().BeGreaterThan(0);

        var saved = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        saved.Should().NotBeNull();
        saved!.Attended.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAttendanceRecordAsync_ShouldUpdateExistingRecord_WhenExists()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = false,
            Notes = "Old note"
        });
        await _context.SaveChangesAsync();

        var updatedAttendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true,
            Notes = "Updated note"
        };

        // Act
        var result = await _sut.SaveAttendanceRecordAsync(updatedAttendance);

        // Assert
        result.Success.Should().BeTrue();

        var records = await _context.Attendances.Where(a => a.MembershipNumber == "M001").ToListAsync();
        records.Should().HaveCount(1); // Should not create duplicate
        records[0].Attended.Should().BeTrue();
        records[0].Notes.Should().Be("Updated note");
    }

    [Fact]
    public async Task SaveAttendanceRecordAsync_ShouldFail_WhenMeetingNotFound()
    {
        // Arrange
        var attendance = new Attendance
        {
            MeetingId = 999,
            MembershipNumber = "M001",
            Attended = true
        };

        // Act
        var result = await _sut.SaveAttendanceRecordAsync(attendance);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Meeting not found");
    }

    [Fact]
    public async Task SaveAttendanceRecordAsync_ShouldFail_WhenMembershipNumberEmpty()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "",
            Attended = true
        };

        // Act
        var result = await _sut.SaveAttendanceRecordAsync(attendance);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Membership number is required");
    }

    #endregion

    #region SaveBulkAttendanceAsync Tests

    [Fact]
    public async Task SaveBulkAttendanceAsync_ShouldCreateMultipleRecords()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        var attendances = new List<Attendance>
        {
            new() { MembershipNumber = "M001", Attended = true },
            new() { MembershipNumber = "M002", Attended = false },
            new() { MembershipNumber = "M003", Attended = true }
        };

        // Act
        var result = await _sut.SaveBulkAttendanceAsync(meeting.Id, attendances);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordsSaved.Should().Be(3);

        var saved = await _context.Attendances.Where(a => a.MeetingId == meeting.Id).ToListAsync();
        saved.Should().HaveCount(3);
    }

    [Fact]
    public async Task SaveBulkAttendanceAsync_ShouldUpdateExistingAndCreateNew()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = false
        });
        await _context.SaveChangesAsync();

        var attendances = new List<Attendance>
        {
            new() { MembershipNumber = "M001", Attended = true },  // Update
            new() { MembershipNumber = "M002", Attended = true }   // Create
        };

        // Act
        var result = await _sut.SaveBulkAttendanceAsync(meeting.Id, attendances);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordsSaved.Should().Be(2);

        var saved = await _context.Attendances.Where(a => a.MeetingId == meeting.Id).ToListAsync();
        saved.Should().HaveCount(2);
        saved.First(a => a.MembershipNumber == "M001").Attended.Should().BeTrue();
    }

    [Fact]
    public async Task SaveBulkAttendanceAsync_ShouldFail_WhenMeetingNotFound()
    {
        // Arrange
        var attendances = new List<Attendance>
        {
            new() { MembershipNumber = "M001", Attended = true }
        };

        // Act
        var result = await _sut.SaveBulkAttendanceAsync(999, attendances);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Meeting not found");
    }

    [Fact]
    public async Task SaveBulkAttendanceAsync_ShouldFail_WhenEmptyList()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        // Act
        var result = await _sut.SaveBulkAttendanceAsync(meeting.Id, new List<Attendance>());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No attendance records");
    }

    #endregion

    #region SignUpMemberForMeetingAsync Tests

    [Fact]
    public async Task SignUpMemberForMeetingAsync_ShouldCreateNewRecord()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        // Act
        var result = await _sut.SignUpMemberForMeetingAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        record.Should().NotBeNull();
        record!.SignedUp.Should().BeTrue();
        record.Attended.Should().BeFalse();
    }

    [Fact]
    public async Task SignUpMemberForMeetingAsync_ShouldUpdateExistingRecord()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            SignedUp = false
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SignUpMemberForMeetingAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeTrue();

        var records = await _context.Attendances.Where(a => a.MembershipNumber == "M001").ToListAsync();
        records.Should().HaveCount(1);
        records[0].SignedUp.Should().BeTrue();
    }

    #endregion

    #region RemoveSignUpAsync Tests

    [Fact]
    public async Task RemoveSignUpAsync_ShouldDeleteRecord_WhenOnlySignUpInfo()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            SignedUp = true,
            Attended = false,
            ConsentEmailReceived = false,
            ConsentFormReceived = false
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.RemoveSignUpAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        record.Should().BeNull();
    }

    [Fact]
    public async Task RemoveSignUpAsync_ShouldClearFlag_WhenHasOtherInfo()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            SignedUp = true,
            Attended = true  // Has attendance info
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.RemoveSignUpAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        record.Should().NotBeNull();
        record!.SignedUp.Should().BeFalse();
        record.Attended.Should().BeTrue(); // Preserved
    }

    [Fact]
    public async Task RemoveSignUpAsync_ShouldFail_WhenRecordNotFound()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        // Act
        var result = await _sut.RemoveSignUpAsync(meeting.Id, "M001");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    #endregion

    #region UpdateConsentEmailStatusAsync Tests

    [Fact]
    public async Task UpdateConsentEmailStatusAsync_ShouldCreateNewRecord()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        var emailDate = new DateTime(2026, 1, 10);

        // Act
        var result = await _sut.UpdateConsentEmailStatusAsync(meeting.Id, "M001", true, emailDate);

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        record.Should().NotBeNull();
        record!.ConsentEmailReceived.Should().BeTrue();
        record.ConsentEmailDate.Should().Be(emailDate);
    }

    [Fact]
    public async Task UpdateConsentEmailStatusAsync_ShouldUpdateExistingRecord()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            ConsentEmailReceived = false
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateConsentEmailStatusAsync(meeting.Id, "M001", true);

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        record!.ConsentEmailReceived.Should().BeTrue();
        record.ConsentEmailDate.Should().Be(DateTime.Today);
    }

    [Fact]
    public async Task UpdateConsentEmailStatusAsync_ShouldClearDate_WhenSettingToFalse()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            ConsentEmailReceived = true,
            ConsentEmailDate = new DateTime(2026, 1, 10)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateConsentEmailStatusAsync(meeting.Id, "M001", false);

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        record!.ConsentEmailReceived.Should().BeFalse();
        record.ConsentEmailDate.Should().BeNull();
    }

    #endregion

    #region UpdateConsentFormStatusAsync Tests

    [Fact]
    public async Task UpdateConsentFormStatusAsync_ShouldCreateNewRecord()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        var formDate = new DateTime(2026, 1, 15);

        // Act
        var result = await _sut.UpdateConsentFormStatusAsync(meeting.Id, "M001", true, formDate);

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.MembershipNumber == "M001");
        record.Should().NotBeNull();
        record!.ConsentFormReceived.Should().BeTrue();
        record.ConsentFormDate.Should().Be(formDate);
    }

    #endregion

    #region GetMembersWithOutstandingConsentAsync Tests

    [Fact]
    public async Task GetMembersWithOutstandingConsentAsync_ShouldReturnMembersWithEmailButNoForm()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", ConsentEmailReceived = true, ConsentFormReceived = false },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M002", ConsentEmailReceived = true, ConsentFormReceived = true },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M003", ConsentEmailReceived = false, ConsentFormReceived = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetMembersWithOutstandingConsentAsync(meeting.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].MembershipNumber.Should().Be("M001");
    }

    #endregion

    #region GetMembersNeedingConsentAsync Tests

    [Fact]
    public async Task GetMembersNeedingConsentAsync_ShouldReturnMembersWithoutEmail()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", ConsentEmailReceived = false },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M002", ConsentEmailReceived = true },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M003", ConsentEmailReceived = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetMembersNeedingConsentAsync(meeting.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.MembershipNumber).Should().Contain("M001", "M003");
    }

    #endregion

    #region GetMeetingAttendanceStatsAsync Tests

    [Fact]
    public async Task GetMeetingAttendanceStatsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", Attended = true, SignedUp = true, ConsentEmailReceived = true, ConsentFormReceived = true },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M002", Attended = true, SignedUp = true, ConsentEmailReceived = true, ConsentFormReceived = false },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M003", Attended = false, SignedUp = true, ConsentEmailReceived = false, ConsentFormReceived = false },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M004", Attended = false, SignedUp = false, ConsentEmailReceived = false, ConsentFormReceived = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetMeetingAttendanceStatsAsync(meeting.Id);

        // Assert
        result.MeetingId.Should().Be(meeting.Id);
        result.TotalMembers.Should().Be(4);
        result.Attended.Should().Be(2);
        result.NotAttended.Should().Be(2);
        result.SignedUp.Should().Be(3);
        result.ConsentEmailReceived.Should().Be(2);
        result.ConsentFormReceived.Should().Be(1);
        result.OutstandingConsent.Should().Be(1);
        result.HasBeenRecorded.Should().BeTrue();
        result.AttendancePercent.Should().Be(50);
    }

    [Fact]
    public async Task GetMeetingAttendanceStatsAsync_ShouldReturnZeros_WhenNoAttendance()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        // Act
        var result = await _sut.GetMeetingAttendanceStatsAsync(meeting.Id);

        // Assert
        result.TotalMembers.Should().Be(0);
        result.HasBeenRecorded.Should().BeFalse();
        result.AttendancePercent.Should().Be(0);
    }

    #endregion

    #region GetMemberAttendanceStatsAsync Tests

    [Fact]
    public async Task GetMemberAttendanceStatsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var term = await CreateTestTermAsync(new DateTime(2026, 1, 6), new DateTime(2026, 4, 3));
        var meeting1 = await CreateTestMeetingAsync(new DateTime(2026, 1, 15));
        var meeting2 = await CreateTestMeetingAsync(new DateTime(2026, 1, 22));
        var meeting3 = await CreateTestMeetingAsync(new DateTime(2026, 1, 29));

        var person = await CreateTestPersonAsync("M001");

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting2.Id, MembershipNumber = "M001", Attended = false },
            new Attendance { MeetingId = meeting3.Id, MembershipNumber = "M001", Attended = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetMemberAttendanceStatsAsync("M001", term.Id);

        // Assert
        result.MembershipNumber.Should().Be("M001");
        result.MemberName.Should().Be("Test Person M001");
        result.TotalMeetings.Should().Be(3);
        result.MeetingsAttended.Should().Be(2);
        result.MeetingsMissed.Should().Be(1);
        result.AttendancePercent.Should().BeApproximately(66.67, 0.01);
    }

    #endregion

    #region GetFullTermAbsencesAsync Tests

    [Fact]
    public async Task GetFullTermAbsencesAsync_ShouldReturnMembersWithNoAttendance()
    {
        // Arrange
        var term = await CreateTestTermAsync(new DateTime(2026, 1, 6), new DateTime(2026, 4, 3));

        // Create meetings in the past (within term)
        var meeting1 = await CreateTestMeetingAsync(DateTime.Today.AddDays(-14));
        var meeting2 = await CreateTestMeetingAsync(DateTime.Today.AddDays(-7));

        // Create members who joined before term started
        var person1 = await CreateTestPersonAsync("M001");
        person1.DateJoined = new DateTime(2024, 1, 1);
        var person2 = await CreateTestPersonAsync("M002");
        person2.DateJoined = new DateTime(2024, 1, 1);
        await _context.SaveChangesAsync();

        // Person 1 attended some meetings, Person 2 attended none
        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting2.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M002", Attended = false },
            new Attendance { MeetingId = meeting2.Id, MembershipNumber = "M002", Attended = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetFullTermAbsencesAsync(term.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].MembershipNumber.Should().Be("M002");
        result[0].AlertType.Should().Be("FullTermAbsence");
    }

    [Fact]
    public async Task GetFullTermAbsencesAsync_ShouldExcludeLeaders()
    {
        // Arrange
        var term = await CreateTestTermAsync(new DateTime(2026, 1, 6), new DateTime(2026, 4, 3));
        var meeting = await CreateTestMeetingAsync(DateTime.Today.AddDays(-7));

        var leader = await CreateTestPersonAsync("L001", PersonType.Leader);
        leader.DateJoined = new DateTime(2024, 1, 1);
        await _context.SaveChangesAsync();

        _context.Attendances.Add(new Attendance { MeetingId = meeting.Id, MembershipNumber = "L001", Attended = false });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetFullTermAbsencesAsync(term.Id);

        // Assert
        result.Should().BeEmpty(); // Leaders are not included
    }

    [Fact]
    public async Task GetFullTermAbsencesAsync_ShouldExcludeNewMembers()
    {
        // Arrange
        var term = await CreateTestTermAsync(new DateTime(2026, 1, 6), new DateTime(2026, 4, 3));
        var meeting = await CreateTestMeetingAsync(DateTime.Today.AddDays(-7));

        var newMember = await CreateTestPersonAsync("M001");
        newMember.DateJoined = new DateTime(2026, 2, 1); // Joined after term started
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetFullTermAbsencesAsync(term.Id);

        // Assert
        result.Should().BeEmpty(); // New members not included
    }

    #endregion

    #region GetLowAttendanceAlertsAsync Tests

    [Fact]
    public async Task GetLowAttendanceAlertsAsync_ShouldReturnMembersWithLowAttendance()
    {
        // Arrange - Use a past term so all meetings are in the past
        var term = await CreateTestTermAsync(new DateTime(2025, 9, 1), new DateTime(2025, 12, 20));

        // Create 10 meetings within the term (all in the past)
        var meetings = new List<Meeting>();
        var baseDate = new DateTime(2025, 9, 4); // First Wednesday in term
        for (int i = 0; i < 10; i++)
        {
            meetings.Add(await CreateTestMeetingAsync(baseDate.AddDays(i * 7)));
        }

        var person1 = await CreateTestPersonAsync("M001");
        person1.DateJoined = new DateTime(2024, 1, 1);
        var person2 = await CreateTestPersonAsync("M002");
        person2.DateJoined = new DateTime(2024, 1, 1);
        await _context.SaveChangesAsync();

        // M001 attended 2/10 (20%), M002 attended 8/10 (80%)
        foreach (var meeting in meetings)
        {
            var m1Attended = meetings.IndexOf(meeting) < 2;
            var m2Attended = meetings.IndexOf(meeting) < 8;

            _context.Attendances.AddRange(
                new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", Attended = m1Attended },
                new Attendance { MeetingId = meeting.Id, MembershipNumber = "M002", Attended = m2Attended }
            );
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetLowAttendanceAlertsAsync(term.Id, 25);

        // Assert
        result.Should().HaveCount(1);
        result[0].MembershipNumber.Should().Be("M001");
        result[0].AttendancePercent.Should().Be(20);
        result[0].AlertType.Should().Be("LowAttendance");
    }

    #endregion

    #region HasAttendanceBeenRecordedAsync Tests

    [Fact]
    public async Task HasAttendanceBeenRecordedAsync_ShouldReturnFalse_WhenNoRecords()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        // Act
        var result = await _sut.HasAttendanceBeenRecordedAsync(meeting.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAttendanceBeenRecordedAsync_ShouldReturnTrue_WhenRecordsExist()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.Add(new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", Attended = true });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.HasAttendanceBeenRecordedAsync(meeting.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetAttendanceCountAsync Tests

    [Fact]
    public async Task GetAttendanceCountAsync_ShouldReturnOnlyAttendedCount()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", Attended = true },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M002", Attended = false },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M003", Attended = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAttendanceCountAsync(meeting.Id);

        // Assert
        result.Should().Be(2);
    }

    #endregion

    #region MeetingRequiresConsentAsync Tests

    [Fact]
    public async Task MeetingRequiresConsentAsync_ShouldReturnTrue_WhenActivityRequiresConsent()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Activities.Add(new Activity
        {
            MeetingId = meeting.Id,
            Name = "Camping",
            RequiresConsent = true
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MeetingRequiresConsentAsync(meeting.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task MeetingRequiresConsentAsync_ShouldReturnFalse_WhenNoActivityRequiresConsent()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        _context.Activities.Add(new Activity
        {
            MeetingId = meeting.Id,
            Name = "Badge Work",
            RequiresConsent = false
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MeetingRequiresConsentAsync(meeting.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region InitializeAttendanceForMeetingAsync Tests

    [Fact]
    public async Task InitializeAttendanceForMeetingAsync_ShouldCreateRecordsForAllActiveMembers()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        await CreateTestPersonAsync("M001");
        await CreateTestPersonAsync("M002");
        await CreateTestPersonAsync("M003", PersonType.Leader);
        await CreateTestPersonAsync("M004", PersonType.Girl, isActive: false); // Inactive

        // Act
        var result = await _sut.InitializeAttendanceForMeetingAsync(meeting.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordsCreated.Should().Be(3); // 2 girls + 1 leader (not inactive)

        var records = await _context.Attendances.Where(a => a.MeetingId == meeting.Id).ToListAsync();
        records.Should().HaveCount(3);
        records.Should().OnlyContain(r => r.Attended == false && r.SignedUp == false);
    }

    [Fact]
    public async Task InitializeAttendanceForMeetingAsync_ShouldSkipExistingRecords()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        await CreateTestPersonAsync("M001");
        await CreateTestPersonAsync("M002");

        // Add existing record for M001
        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.InitializeAttendanceForMeetingAsync(meeting.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordsCreated.Should().Be(1); // Only M002

        var records = await _context.Attendances.Where(a => a.MeetingId == meeting.Id).ToListAsync();
        records.Should().HaveCount(2);

        // M001's record should still be marked as attended
        var m001Record = records.First(r => r.MembershipNumber == "M001");
        m001Record.Attended.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAttendanceForMeetingAsync_ShouldFail_WhenMeetingNotFound()
    {
        // Act
        var result = await _sut.InitializeAttendanceForMeetingAsync(999);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Meeting not found");
    }

    #endregion

    #region Nights Away Tracking Tests

    [Fact]
    public async Task SaveAttendanceRecordAsync_ShouldAutoCalculateNightsAway_ForMultiDayMeeting()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 7), // 2 nights
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp",
            LocationName = "Camp Site"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true
        };

        // Act
        var result = await _sut.SaveAttendanceRecordAsync(attendance);

        // Assert
        result.Success.Should().BeTrue();
        result.Attendance!.NightsAway.Should().Be(2);
    }

    [Fact]
    public async Task SaveAttendanceRecordAsync_ShouldNotSetNightsAway_ForSingleDayMeeting()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();

        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true
        };

        // Act
        var result = await _sut.SaveAttendanceRecordAsync(attendance);

        // Assert
        result.Success.Should().BeTrue();
        result.Attendance!.NightsAway.Should().BeNull();
    }

    [Fact]
    public async Task SaveAttendanceRecordAsync_ShouldClearNightsAway_WhenNotAttended()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 7),
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp",
            LocationName = "Camp Site"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        _context.Attendances.Add(new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true,
            NightsAway = 2
        });
        await _context.SaveChangesAsync();

        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = false // Now not attending
        };

        // Act
        var result = await _sut.SaveAttendanceRecordAsync(attendance);

        // Assert
        result.Success.Should().BeTrue();
        result.Attendance!.NightsAway.Should().BeNull();
    }

    [Fact]
    public async Task UpdateNightsAwayAsync_ShouldAllowManualOverride()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 7),
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp",
            LocationName = "Camp Site"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true,
            NightsAway = 2
        };
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act - Member left early, only stayed 1 night
        var result = await _sut.UpdateNightsAwayAsync(attendance.Id, 1);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Attendances.FindAsync(attendance.Id);
        updated!.NightsAway.Should().Be(1);
    }

    [Fact]
    public async Task UpdateNightsAwayAsync_ShouldFail_WhenNegativeNights()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true
        };
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateNightsAwayAsync(attendance.Id, -1);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task GetTotalNightsAwayAsync_ShouldSumAllAttendedMeetings()
    {
        // Arrange
        var meeting1 = new Meeting
        {
            Date = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 7),
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp 1",
            LocationName = "Camp Site"
        };
        var meeting2 = new Meeting
        {
            Date = new DateTime(2026, 2, 10),
            EndDate = new DateTime(2026, 2, 11),
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp 2",
            LocationName = "Camp Site"
        };
        _context.Meetings.AddRange(meeting1, meeting2);
        await _context.SaveChangesAsync();

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M001", Attended = true, NightsAway = 2 },
            new Attendance { MeetingId = meeting2.Id, MembershipNumber = "M001", Attended = true, NightsAway = 1 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetTotalNightsAwayAsync("M001");

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task GetTotalNightsAwayAsync_ShouldReturnZero_WhenNoAttendance()
    {
        // Act
        var result = await _sut.GetTotalNightsAwayAsync("M001");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetNightsAwayInRangeAsync_ShouldFilterByDateRange()
    {
        // Arrange
        var meeting1 = new Meeting
        {
            Date = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 7),
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp 1",
            LocationName = "Camp Site"
        };
        var meeting2 = new Meeting
        {
            Date = new DateTime(2026, 3, 10),
            EndDate = new DateTime(2026, 3, 11),
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp 2",
            LocationName = "Camp Site"
        };
        _context.Meetings.AddRange(meeting1, meeting2);
        await _context.SaveChangesAsync();

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting1.Id, MembershipNumber = "M001", Attended = true, NightsAway = 2 },
            new Attendance { MeetingId = meeting2.Id, MembershipNumber = "M001", Attended = true, NightsAway = 1 }
        );
        await _context.SaveChangesAsync();

        // Act - Only get January
        var result = await _sut.GetNightsAwayInRangeAsync("M001", new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetNightsAwaySummaryAsync_ShouldIncludeAllMemberTypes()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 7),
            StartTime = new TimeOnly(10, 00),
            EndTime = new TimeOnly(16, 00),
            MeetingType = MeetingType.Extra,
            Title = "Camp",
            LocationName = "Camp Site"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        await CreateTestPersonAsync("M001", PersonType.Girl);
        await CreateTestPersonAsync("L001", PersonType.Leader);

        _context.Attendances.AddRange(
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "M001", Attended = true, NightsAway = 2 },
            new Attendance { MeetingId = meeting.Id, MembershipNumber = "L001", Attended = true, NightsAway = 2 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetNightsAwaySummaryAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.PersonType == "Girl");
        result.Should().Contain(s => s.PersonType == "Leader");
    }

    #endregion

    #region DeleteAttendanceRecordAsync Tests

    [Fact]
    public async Task DeleteAttendanceRecordAsync_ShouldDeleteRecord()
    {
        // Arrange
        var meeting = await CreateTestMeetingAsync();
        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "M001",
            Attended = true
        };
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAttendanceRecordAsync(attendance.Id);

        // Assert
        result.Success.Should().BeTrue();

        var record = await _context.Attendances.FindAsync(attendance.Id);
        record.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAttendanceRecordAsync_ShouldFail_WhenRecordNotFound()
    {
        // Act
        var result = await _sut.DeleteAttendanceRecordAsync(999);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    #endregion
}

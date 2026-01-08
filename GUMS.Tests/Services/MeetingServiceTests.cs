using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GUMS.Tests.Services;

public class MeetingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<ITermService> _mockTermService;
    private readonly MeetingService _sut; // System Under Test

    public MeetingServiceTests()
    {
        // Arrange - Create in-memory database for each test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        // Mock configuration service
        _mockConfigService = new Mock<IConfigurationService>();
        var defaultConfig = new UnitConfiguration
        {
            Id = 1,
            UnitName = "Test Unit",
            UnitType = Section.Brownie,
            MeetingDayOfWeek = DayOfWeek.Wednesday,
            DefaultMeetingStartTime = new TimeOnly(18, 30),
            DefaultMeetingEndTime = new TimeOnly(19, 30),
            DefaultLocationName = "Village Hall",
            DefaultLocationAddress = "123 Main Street",
            DefaultSubsAmount = 20.00m,
            PaymentTermDays = 14
        };
        _mockConfigService.Setup(x => x.GetConfigurationAsync())
            .ReturnsAsync(defaultConfig);

        // Mock term service
        _mockTermService = new Mock<ITermService>();

        _sut = new MeetingService(_context, _mockConfigService.Object, _mockTermService.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoMeetingsExist()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllMeetings_OrderedByDateDescending()
    {
        // Arrange
        var meeting1 = new Meeting
        {
            Date = new DateTime(2026, 1, 15),
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Meeting 1",
            LocationName = "Hall"
        };

        var meeting2 = new Meeting
        {
            Date = new DateTime(2026, 3, 15),
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Meeting 2",
            LocationName = "Hall"
        };

        var meeting3 = new Meeting
        {
            Date = new DateTime(2026, 2, 15),
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Extra,
            Title = "Meeting 3",
            LocationName = "Hall"
        };

        _context.Meetings.AddRange(meeting1, meeting2, meeting3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Meeting 2"); // Most recent first
        result[1].Title.Should().Be("Meeting 3");
        result[2].Title.Should().Be("Meeting 1");
    }

    [Fact]
    public async Task GetAllAsync_ShouldIncludeActivities()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Test Meeting",
            LocationName = "Hall",
            Activities = new List<Activity>
            {
                new() { Name = "Activity 1", SortOrder = 0 },
                new() { Name = "Activity 2", SortOrder = 1 }
            }
        };

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Activities.Should().HaveCount(2);
        result[0].Activities[0].Name.Should().Be("Activity 1");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnMeeting_WhenExists()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Test Meeting",
            LocationName = "Hall"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(meeting.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Meeting");
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

    #region GetByDateRangeAsync Tests

    [Fact]
    public async Task GetByDateRangeAsync_ShouldReturnMeetingsInRange_OrderedByDateAscending()
    {
        // Arrange
        var meeting1 = CreateMeeting("Meeting 1", new DateTime(2026, 1, 15));
        var meeting2 = CreateMeeting("Meeting 2", new DateTime(2026, 2, 15));
        var meeting3 = CreateMeeting("Meeting 3", new DateTime(2026, 3, 15));
        var meeting4 = CreateMeeting("Meeting 4", new DateTime(2026, 4, 15));

        _context.Meetings.AddRange(meeting1, meeting2, meeting3, meeting4);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByDateRangeAsync(new DateTime(2026, 2, 1), new DateTime(2026, 3, 31));

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Meeting 2"); // Earliest first
        result[1].Title.Should().Be("Meeting 3");
    }

    #endregion

    #region GetUpcomingAsync Tests

    [Fact]
    public async Task GetUpcomingAsync_ShouldReturnFutureMeetings_OrderedByDateAscending()
    {
        // Arrange
        var today = DateTime.Today;
        var pastMeeting = CreateMeeting("Past", today.AddDays(-10));
        var todayMeeting = CreateMeeting("Today", today);
        var future1 = CreateMeeting("Future 1", today.AddDays(5));
        var future2 = CreateMeeting("Future 2", today.AddDays(10));

        _context.Meetings.AddRange(pastMeeting, todayMeeting, future1, future2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetUpcomingAsync();

        // Assert
        result.Should().HaveCount(3); // Today and future meetings
        result[0].Title.Should().Be("Today");
        result[1].Title.Should().Be("Future 1");
        result[2].Title.Should().Be("Future 2");
    }

    [Fact]
    public async Task GetUpcomingAsync_ShouldRespectLimit()
    {
        // Arrange
        var today = DateTime.Today;
        _context.Meetings.AddRange(
            CreateMeeting("Meeting 1", today.AddDays(1)),
            CreateMeeting("Meeting 2", today.AddDays(2)),
            CreateMeeting("Meeting 3", today.AddDays(3))
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetUpcomingAsync(limit: 2);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetPastAsync Tests

    [Fact]
    public async Task GetPastAsync_ShouldReturnPastMeetings_OrderedByDateDescending()
    {
        // Arrange
        var today = DateTime.Today;
        var past1 = CreateMeeting("Past 1", today.AddDays(-20));
        var past2 = CreateMeeting("Past 2", today.AddDays(-10));
        var future = CreateMeeting("Future", today.AddDays(10));

        _context.Meetings.AddRange(past1, past2, future);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetPastAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Past 2"); // Most recent past first
        result[1].Title.Should().Be("Past 1");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateMeeting_WhenValid()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today.AddDays(7),
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "New Meeting",
            LocationName = "Hall"
        };

        // Act
        var result = await _sut.CreateAsync(meeting);

        // Assert
        result.Success.Should().BeTrue();
        result.Meeting.Should().NotBeNull();
        result.Meeting!.Id.Should().BeGreaterThan(0);

        var saved = await _context.Meetings.FindAsync(result.Meeting.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldSetSortOrderForActivities()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Meeting with Activities",
            LocationName = "Hall",
            Activities = new List<Activity>
            {
                new() { Name = "Activity 1" },
                new() { Name = "Activity 2" },
                new() { Name = "Activity 3" }
            }
        };

        // Act
        var result = await _sut.CreateAsync(meeting);

        // Assert
        result.Success.Should().BeTrue();
        result.Meeting!.Activities[0].SortOrder.Should().Be(0);
        result.Meeting.Activities[1].SortOrder.Should().Be(1);
        result.Meeting.Activities[2].SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenEndTimeBeforeStartTime()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(19, 30),
            EndTime = new TimeOnly(18, 30), // Before start time
            MeetingType = MeetingType.Regular,
            Title = "Invalid Meeting",
            LocationName = "Hall"
        };

        // Act
        var result = await _sut.CreateAsync(meeting);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("End time must be after start time");
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenCostIsNegative()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Extra,
            Title = "Paid Meeting",
            LocationName = "Hall",
            CostPerAttendee = -10
        };

        // Act
        var result = await _sut.CreateAsync(meeting);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenCostWithoutPaymentDeadline()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Extra,
            Title = "Paid Meeting",
            LocationName = "Hall",
            CostPerAttendee = 50,
            PaymentDeadline = null
        };

        // Act
        var result = await _sut.CreateAsync(meeting);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Payment deadline is required");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMeeting_WhenValid()
    {
        // Arrange
        var meeting = CreateMeeting("Original Title", DateTime.Today);
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        meeting.Title = "Updated Title";
        meeting.Description = "New Description";

        // Act
        var result = await _sut.UpdateAsync(meeting);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Meetings.FindAsync(meeting.Id);
        updated!.Title.Should().Be("Updated Title");
        updated.Description.Should().Be("New Description");
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenMeetingNotFound()
    {
        // Arrange
        var meeting = new Meeting
        {
            Id = 999,
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Non-existent",
            LocationName = "Hall"
        };

        // Act
        var result = await _sut.UpdateAsync(meeting);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteMeeting_WhenNoAttendance()
    {
        // Arrange
        var meeting = CreateMeeting("To Delete", DateTime.Today);
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(meeting.Id);

        // Assert
        result.Success.Should().BeTrue();

        var deleted = await _context.Meetings.FindAsync(meeting.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteActivities_WhenDeletingMeeting()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Meeting with Activities",
            LocationName = "Hall",
            Activities = new List<Activity>
            {
                new() { Name = "Activity 1" },
                new() { Name = "Activity 2" }
            }
        };

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var activityCount = await _context.Activities.CountAsync();
        activityCount.Should().Be(2);

        // Act
        var result = await _sut.DeleteAsync(meeting.Id);

        // Assert
        result.Success.Should().BeTrue();

        var remainingActivities = await _context.Activities.CountAsync();
        remainingActivities.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenAttendanceExists()
    {
        // Arrange
        var meeting = CreateMeeting("Meeting with Attendance", DateTime.Today);
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var attendance = new Attendance
        {
            MeetingId = meeting.Id,
            MembershipNumber = "12345",
            Attended = true
        };
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(meeting.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("attendance has been recorded");
    }

    #endregion

    #region Activity Management Tests

    [Fact]
    public async Task GetActivitiesForMeetingAsync_ShouldReturnActivities_OrderedBySortOrder()
    {
        // Arrange
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Meeting",
            LocationName = "Hall",
            Activities = new List<Activity>
            {
                new() { Name = "Activity 2", SortOrder = 1 },
                new() { Name = "Activity 1", SortOrder = 0 },
                new() { Name = "Activity 3", SortOrder = 2 }
            }
        };

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetActivitiesForMeetingAsync(meeting.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Activity 1");
        result[1].Name.Should().Be("Activity 2");
        result[2].Name.Should().Be("Activity 3");
    }

    [Fact]
    public async Task AddActivityAsync_ShouldAddActivity_WithCorrectSortOrder()
    {
        // Arrange
        var meeting = CreateMeeting("Meeting", DateTime.Today);
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var activity1 = new Activity { MeetingId = meeting.Id, Name = "First Activity" };
        var activity2 = new Activity { MeetingId = meeting.Id, Name = "Second Activity" };

        // Act
        var result1 = await _sut.AddActivityAsync(activity1);
        var result2 = await _sut.AddActivityAsync(activity2);

        // Assert
        result1.Success.Should().BeTrue();
        result1.Activity!.SortOrder.Should().Be(0);

        result2.Success.Should().BeTrue();
        result2.Activity!.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task AddActivityAsync_ShouldFail_WhenMeetingNotFound()
    {
        // Arrange
        var activity = new Activity { MeetingId = 999, Name = "Activity" };

        // Act
        var result = await _sut.AddActivityAsync(activity);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Meeting not found");
    }

    [Fact]
    public async Task UpdateActivityAsync_ShouldUpdateActivity()
    {
        // Arrange
        var meeting = CreateMeeting("Meeting", DateTime.Today);
        meeting.Activities.Add(new Activity { Name = "Original Name" });
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var activity = meeting.Activities[0];
        activity.Name = "Updated Name";
        activity.RequiresConsent = true;

        // Act
        var result = await _sut.UpdateActivityAsync(activity);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.Activities.FindAsync(activity.Id);
        updated!.Name.Should().Be("Updated Name");
        updated.RequiresConsent.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteActivityAsync_ShouldDeleteActivity()
    {
        // Arrange
        var meeting = CreateMeeting("Meeting", DateTime.Today);
        meeting.Activities.Add(new Activity { Name = "To Delete" });
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var activityId = meeting.Activities[0].Id;

        // Act
        var result = await _sut.DeleteActivityAsync(activityId);

        // Assert
        result.Success.Should().BeTrue();

        var deleted = await _context.Activities.FindAsync(activityId);
        deleted.Should().BeNull();
    }

    #endregion

    #region Meeting Generation Tests

    [Fact]
    public async Task GetSuggestedMeetingDatesForTermAsync_ShouldReturnCorrectDates()
    {
        // Arrange
        var term = new Term
        {
            Name = "Autumn 2026",
            StartDate = new DateTime(2026, 9, 1), // Tuesday
            EndDate = new DateTime(2026, 12, 20),
            SubsAmount = 20
        };

        _mockTermService.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(term);

        // Act - Looking for Wednesdays (configured day)
        var result = await _sut.GetSuggestedMeetingDatesForTermAsync(1);

        // Assert
        result.Should().NotBeEmpty();
        result.All(d => d.DayOfWeek == DayOfWeek.Wednesday).Should().BeTrue();
        result.All(d => d >= term.StartDate && d <= term.EndDate).Should().BeTrue();
        result.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetSuggestedMeetingDatesForTermAsync_ShouldReturnEmpty_WhenTermNotFound()
    {
        // Arrange
        _mockTermService.Setup(x => x.GetByIdAsync(999))
            .ReturnsAsync((Term?)null);

        // Act
        var result = await _sut.GetSuggestedMeetingDatesForTermAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateRegularMeetingsForTermAsync_ShouldCreateMeetings()
    {
        // Arrange
        var term = new Term
        {
            Name = "Autumn 2026",
            StartDate = new DateTime(2026, 9, 2), // Wednesday
            EndDate = new DateTime(2026, 9, 23),  // 4 Wednesdays
            SubsAmount = 20
        };

        _mockTermService.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(term);

        // Act
        var result = await _sut.GenerateRegularMeetingsForTermAsync(1, "Test Meeting");

        // Assert
        result.Success.Should().BeTrue();
        result.MeetingsCreated.Should().Be(4);

        var meetings = await _context.Meetings.ToListAsync();
        meetings.Should().HaveCount(4);
        meetings.All(m => m.MeetingType == MeetingType.Regular).Should().BeTrue();
        meetings.All(m => m.Title == "Test Meeting").Should().BeTrue();
    }

    [Fact]
    public async Task GenerateRegularMeetingsForTermAsync_ShouldSkipExistingDates()
    {
        // Arrange
        var term = new Term
        {
            Name = "Autumn 2026",
            StartDate = new DateTime(2026, 9, 2),
            EndDate = new DateTime(2026, 9, 23),
            SubsAmount = 20
        };

        _mockTermService.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(term);

        // Create a meeting on the first Wednesday
        var existingMeeting = CreateMeeting("Existing", new DateTime(2026, 9, 2));
        _context.Meetings.Add(existingMeeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GenerateRegularMeetingsForTermAsync(1);

        // Assert
        result.Success.Should().BeTrue();
        result.MeetingsCreated.Should().Be(3); // Only 3 new meetings

        var allMeetings = await _context.Meetings.ToListAsync();
        allMeetings.Should().HaveCount(4); // 1 existing + 3 new
    }

    #endregion

    #region Query Helper Tests

    [Fact]
    public async Task MeetingExistsOnDateAsync_ShouldReturnTrue_WhenMeetingExists()
    {
        // Arrange
        var meeting = CreateMeeting("Meeting", new DateTime(2026, 5, 15));
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MeetingExistsOnDateAsync(new DateTime(2026, 5, 15));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task MeetingExistsOnDateAsync_ShouldReturnFalse_WhenNoMeeting()
    {
        // Act
        var result = await _sut.MeetingExistsOnDateAsync(new DateTime(2026, 5, 15));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetNextMeetingDateAsync_ShouldReturnNextDate()
    {
        // Arrange
        var today = DateTime.Today;
        _context.Meetings.AddRange(
            CreateMeeting("Past", today.AddDays(-5)),
            CreateMeeting("Next", today.AddDays(5)),
            CreateMeeting("Later", today.AddDays(10))
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetNextMeetingDateAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(today.AddDays(5));
    }

    [Fact]
    public async Task GetNextMeetingDateAsync_ShouldReturnNull_WhenNoUpcomingMeetings()
    {
        // Arrange
        var past = CreateMeeting("Past", DateTime.Today.AddDays(-10));
        _context.Meetings.Add(past);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetNextMeetingDateAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMeetingCountInRangeAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        _context.Meetings.AddRange(
            CreateMeeting("M1", new DateTime(2026, 1, 15)),
            CreateMeeting("M2", new DateTime(2026, 2, 15)),
            CreateMeeting("M3", new DateTime(2026, 3, 15)),
            CreateMeeting("M4", new DateTime(2026, 4, 15))
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetMeetingCountInRangeAsync(
            new DateTime(2026, 2, 1),
            new DateTime(2026, 3, 31));

        // Assert
        result.Should().Be(2); // February and March meetings
    }

    #endregion

    #region Helper Methods

    private Meeting CreateMeeting(string title, DateTime date)
    {
        return new Meeting
        {
            Date = date,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = title,
            LocationName = "Hall"
        };
    }

    #endregion
}

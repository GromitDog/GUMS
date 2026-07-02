using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Tests.Services;

public class BudgetServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly BudgetService _sut;

    public BudgetServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _sut = new BudgetService(_context);
    }

    public void Dispose() => _context?.Dispose();

    // ---- Helpers ---------------------------------------------------------

    private async Task AddPeople(int girls, int leaders)
    {
        for (var i = 0; i < girls; i++)
        {
            _context.Persons.Add(new Person
            {
                MembershipNumber = $"G{i}",
                FullName = $"Girl {i}",
                PersonType = PersonType.Girl,
                Section = Section.Brownie,
                DateJoined = new DateTime(2025, 1, 1),
                IsActive = true
            });
        }

        for (var i = 0; i < leaders; i++)
        {
            _context.Persons.Add(new Person
            {
                MembershipNumber = $"L{i}",
                FullName = $"Leader {i}",
                PersonType = PersonType.Leader,
                DateJoined = new DateTime(2025, 1, 1),
                IsActive = true
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task<Meeting> AddMeeting(decimal? costPerAttendee = null, decimal? costPerLeader = null)
    {
        var meeting = new Meeting
        {
            Date = new DateTime(2026, 6, 1),
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Summer Trip",
            LocationName = "Adventure Park",
            CostPerAttendee = costPerAttendee,
            CostPerLeader = costPerLeader
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        return meeting;
    }

    /// <summary>
    /// Creates a budget with the canonical item mix used across the tests:
    /// £5/girl, £3/adult, £2/head, £20 fixed.
    /// </summary>
    private async Task<EventBudget> AddCanonicalBudget(int meetingId)
    {
        var budget = new EventBudget { MeetingId = meetingId };
        _context.EventBudgets.Add(budget);
        await _context.SaveChangesAsync();

        _context.EventBudgetItems.AddRange(
            new EventBudgetItem { EventBudgetId = budget.Id, Description = "Activity", CostType = BudgetCostType.PerGirl, Amount = 5m },
            new EventBudgetItem { EventBudgetId = budget.Id, Description = "Leader place", CostType = BudgetCostType.PerAdult, Amount = 3m },
            new EventBudgetItem { EventBudgetId = budget.Id, Description = "Entry", CostType = BudgetCostType.PerPerson, Amount = 2m },
            new EventBudgetItem { EventBudgetId = budget.Id, Description = "Coach", CostType = BudgetCostType.FixedTotal, Amount = 20m });
        await _context.SaveChangesAsync();
        return budget;
    }

    // ---- GetBudgetEstimateAsync ------------------------------------------

    [Fact]
    public async Task GetBudgetEstimateAsync_ReturnsNull_WhenNoBudget()
    {
        var meeting = await AddMeeting();

        var result = await _sut.GetBudgetEstimateAsync(meeting.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBudgetEstimateAsync_LeadersPay_SplitsCostFairlyAndBreaksEven()
    {
        await AddPeople(girls: 8, leaders: 2);
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.GetBudgetEstimateAsync(meeting.Id, leadersPayOverride: true);

        result.Should().NotBeNull();
        var full = result!.Scenarios.First();
        full.Girls.Should().Be(8);
        full.Adults.Should().Be(2);
        // 5*8 + 3*2 + 2*10 + 20 = 86
        full.TotalCost.Should().Be(86m);
        // per girl = 5 + 2 + (20/10) = 9 ; per adult = 3 + 2 + 2 = 7
        full.CostPerGirl.Should().Be(9m);
        full.CostPerAdult.Should().Be(7m);
        // charges recover the full cost
        (full.CostPerGirl * full.Girls + full.CostPerAdult * full.Adults).Should().Be(full.TotalCost);
    }

    [Fact]
    public async Task GetBudgetEstimateAsync_LeadersDontPay_GirlsCoverAdults()
    {
        await AddPeople(girls: 8, leaders: 2);
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.GetBudgetEstimateAsync(meeting.Id, leadersPayOverride: false);

        var full = result!.Scenarios.First();
        full.TotalCost.Should().Be(86m);
        full.CostPerAdult.Should().Be(0m);
        // whole cost falls on the 8 girls: 86 / 8 = 10.75
        full.CostPerGirl.Should().Be(10.75m);
    }

    [Theory]
    [InlineData(5, true)]   // a leader charge is set -> leaders assumed to pay
    [InlineData(0, false)]  // zero -> leaders don't pay
    [InlineData(null, false)] // unset -> leaders don't pay
    public async Task GetBudgetEstimateAsync_DefaultsLeadersPayFromMeetingLeaderCharge(int? costPerLeader, bool expected)
    {
        await AddPeople(girls: 4, leaders: 2);
        var meeting = await AddMeeting(costPerLeader: costPerLeader);
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.GetBudgetEstimateAsync(meeting.Id);

        result!.LeadersPay.Should().Be(expected);
    }

    [Fact]
    public async Task GetBudgetEstimateAsync_ProjectedIncome_UsesMeetingCharges()
    {
        await AddPeople(girls: 8, leaders: 2);
        var meeting = await AddMeeting(costPerAttendee: 10m, costPerLeader: 5m);
        await AddCanonicalBudget(meeting.Id);

        var whenLeadersPay = await _sut.GetBudgetEstimateAsync(meeting.Id, leadersPayOverride: true);
        whenLeadersPay!.HasCharge.Should().BeTrue();
        var full = whenLeadersPay.Scenarios.First();
        // 10*8 + 5*2 = 90 ; surplus 90 - 86 = 4
        full.ProjectedIncome.Should().Be(90m);
        full.Surplus.Should().Be(4m);

        var whenLeadersFree = await _sut.GetBudgetEstimateAsync(meeting.Id, leadersPayOverride: false);
        var fullFree = whenLeadersFree!.Scenarios.First();
        // leaders excluded from income: 10*8 = 80
        fullFree.ProjectedIncome.Should().Be(80m);
        fullFree.Surplus.Should().Be(80m - 86m);
    }

    [Fact]
    public async Task GetBudgetEstimateAsync_Scenarios_ScaleGirlCounts()
    {
        await AddPeople(girls: 8, leaders: 2);
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.GetBudgetEstimateAsync(meeting.Id, leadersPayOverride: true);

        result!.Scenarios.Should().HaveCount(3);
        result.Scenarios[0].Girls.Should().Be(8); // full
        result.Scenarios[1].Girls.Should().Be(6); // 75%
        result.Scenarios[2].Girls.Should().Be(4); // 50%
        // Likely scenario: total 5*6 + 3*2 + 2*8 + 20 = 72
        result.Scenarios[1].TotalCost.Should().Be(72m);
        result.Scenarios[1].CostPerGirl.Should().Be(9.5m); // 5 + 2 + 20/8
        result.Scenarios[1].CostPerAdult.Should().Be(7.5m); // 3 + 2 + 20/8
    }

    [Fact]
    public async Task GetBudgetEstimateAsync_AdultCountOverride_ChangesSplit()
    {
        await AddPeople(girls: 8, leaders: 2);
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.GetBudgetEstimateAsync(meeting.Id, leadersPayOverride: true, adultCountOverride: 4);

        var full = result!.Scenarios.First();
        full.Adults.Should().Be(4);
        result.AdultCount.Should().Be(4);
        result.ActiveLeaderCount.Should().Be(2);
        // total 5*8 + 3*4 + 2*12 + 20 = 96
        full.TotalCost.Should().Be(96m);
    }

    [Fact]
    public async Task GetBudgetEstimateAsync_UsesStoredPlanningValues_WhenNoOverride()
    {
        await AddPeople(girls: 8, leaders: 2);
        var meeting = await AddMeeting(costPerLeader: 5m); // would default to leadersPay=true
        var budget = await AddCanonicalBudget(meeting.Id);

        budget.LeadersPay = false;
        budget.PlannedAdultCount = 3;
        await _context.SaveChangesAsync();

        var result = await _sut.GetBudgetEstimateAsync(meeting.Id);

        result!.LeadersPay.Should().BeFalse();
        result.AdultCount.Should().Be(3);
    }

    // ---- UpdateBudgetPlanningAsync ---------------------------------------

    [Fact]
    public async Task UpdateBudgetPlanningAsync_PersistsValues()
    {
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.UpdateBudgetPlanningAsync(meeting.Id, leadersPay: true, plannedAdultCount: 4);

        result.Success.Should().BeTrue();
        var stored = await _context.EventBudgets.AsNoTracking().FirstAsync(b => b.MeetingId == meeting.Id);
        stored.LeadersPay.Should().BeTrue();
        stored.PlannedAdultCount.Should().Be(4);
    }

    [Fact]
    public async Task UpdateBudgetPlanningAsync_ReturnsFalse_WhenNoBudget()
    {
        var meeting = await AddMeeting();

        var result = await _sut.UpdateBudgetPlanningAsync(meeting.Id, leadersPay: true, plannedAdultCount: 2);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateBudgetPlanningAsync_ReturnsFalse_WhenAdultCountNegative()
    {
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.UpdateBudgetPlanningAsync(meeting.Id, leadersPay: true, plannedAdultCount: -1);

        result.Success.Should().BeFalse();
    }

    // ---- Income lines ----------------------------------------------------

    [Fact]
    public async Task AddBudgetIncomeAsync_AddsLine_AndGetBudgetIncludesIt()
    {
        var meeting = await AddMeeting();
        var budget = await AddCanonicalBudget(meeting.Id);

        var result = await _sut.AddBudgetIncomeAsync(new EventBudgetIncome
        {
            EventBudgetId = budget.Id,
            Description = "District grant",
            Amount = 50m
        });

        result.Success.Should().BeTrue();
        var loaded = await _sut.GetBudgetForMeetingAsync(meeting.Id);
        loaded!.IncomeItems.Should().ContainSingle();
        loaded.IncomeItems[0].Description.Should().Be("District grant");
        loaded.IncomeItems[0].Amount.Should().Be(50m);
    }

    [Fact]
    public async Task AddBudgetIncomeAsync_ReturnsFalse_WhenDescriptionEmpty()
    {
        var meeting = await AddMeeting();
        var budget = await AddCanonicalBudget(meeting.Id);

        var result = await _sut.AddBudgetIncomeAsync(new EventBudgetIncome
        {
            EventBudgetId = budget.Id,
            Description = "  ",
            Amount = 50m
        });

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBudgetIncomeAsync_RemovesLine()
    {
        var meeting = await AddMeeting();
        var budget = await AddCanonicalBudget(meeting.Id);
        var income = new EventBudgetIncome { EventBudgetId = budget.Id, Description = "Cake sale", Amount = 30m };
        _context.EventBudgetIncomes.Add(income);
        await _context.SaveChangesAsync();

        var result = await _sut.DeleteBudgetIncomeAsync(income.Id);

        result.Success.Should().BeTrue();
        (await _context.EventBudgetIncomes.CountAsync()).Should().Be(0);
    }

    // ---- SaveEventChargesAsync -------------------------------------------

    [Fact]
    public async Task SaveEventChargesAsync_LeadersPay_WritesBothChargesAndPlannedCounts()
    {
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.SaveEventChargesAsync(
            meeting.Id, costPerGirl: 9m, costPerLeader: 7m, plannedGirlCount: 8, plannedAdultCount: 2, leadersPay: true);

        result.Success.Should().BeTrue();
        var savedMeeting = await _context.Meetings.AsNoTracking().FirstAsync(m => m.Id == meeting.Id);
        savedMeeting.CostPerAttendee.Should().Be(9m);
        savedMeeting.CostPerLeader.Should().Be(7m);

        var savedBudget = await _context.EventBudgets.AsNoTracking().FirstAsync(b => b.MeetingId == meeting.Id);
        savedBudget.LeadersPay.Should().BeTrue();
        savedBudget.PlannedGirlCount.Should().Be(8);
        savedBudget.PlannedAdultCount.Should().Be(2);
    }

    [Fact]
    public async Task SaveEventChargesAsync_LeadersDontPay_ClearsLeaderCharge()
    {
        var meeting = await AddMeeting(costPerLeader: 5m);
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.SaveEventChargesAsync(
            meeting.Id, costPerGirl: 10m, costPerLeader: 5m, plannedGirlCount: 8, plannedAdultCount: 2, leadersPay: false);

        result.Success.Should().BeTrue();
        var savedMeeting = await _context.Meetings.AsNoTracking().FirstAsync(m => m.Id == meeting.Id);
        savedMeeting.CostPerAttendee.Should().Be(10m);
        savedMeeting.CostPerLeader.Should().BeNull();
    }

    [Fact]
    public async Task SaveEventChargesAsync_ReturnsFalse_WhenMeetingMissing()
    {
        var result = await _sut.SaveEventChargesAsync(
            meetingId: 999, costPerGirl: 5m, costPerLeader: null, plannedGirlCount: 8, plannedAdultCount: 2, leadersPay: false);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SaveEventChargesAsync_ReturnsFalse_WhenChargeNegative()
    {
        var meeting = await AddMeeting();
        await AddCanonicalBudget(meeting.Id);

        var result = await _sut.SaveEventChargesAsync(
            meeting.Id, costPerGirl: -1m, costPerLeader: null, plannedGirlCount: 8, plannedAdultCount: 2, leadersPay: false);

        result.Success.Should().BeFalse();
    }
}

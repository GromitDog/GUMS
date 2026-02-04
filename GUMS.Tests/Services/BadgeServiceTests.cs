using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Tests.Services;

public class BadgeServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly BadgeService _sut;

    public BadgeServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _sut = new BadgeService(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task CreateBadgeAsync_ShouldCreateBadgeWithClauses()
    {
        var badge = new BadgeDefinition
        {
            Name = "First Aid Stage 1",
            Theme = Theme.BeWell,
            BadgeType = BadgeType.SkillsBuilder,
            Section = Section.Brownie,
            Stage = 1,
            SkillsBuilderName = "First Aid",
            RequiredCompletions = 4,
            Clauses = new List<BadgeClause>
            {
                new() { Name = "Clause 1", Description = "Learn about first aid kits" },
                new() { Name = "Clause 2", Description = "Practice bandaging" },
                new() { Name = "Clause 3", Description = "Call 999" },
                new() { Name = "Clause 4", Description = "Recovery position" },
                new() { Name = "Clause 5", Description = "Burns treatment" }
            }
        };

        var result = await _sut.CreateBadgeAsync(badge);

        result.Success.Should().BeTrue();
        result.Badge.Should().NotBeNull();
        result.Badge!.Id.Should().BeGreaterThan(0);
        result.Badge.Clauses.Should().HaveCount(5);
    }

    [Fact]
    public async Task CreateBadgeAsync_ShouldSetDefaultRequiredCompletions_ForSkillsBuilder()
    {
        var badge = new BadgeDefinition
        {
            Name = "Test Badge",
            Theme = Theme.KnowMyself,
            BadgeType = BadgeType.SkillsBuilder,
            Section = Section.Guide,
            RequiredCompletions = 0
        };

        var result = await _sut.CreateBadgeAsync(badge);

        result.Badge!.RequiredCompletions.Should().Be(4);
    }

    [Fact]
    public async Task CreateBadgeAsync_ShouldSetDefaultRequiredCompletions_ForInterestBadge()
    {
        var badge = new BadgeDefinition
        {
            Name = "Baking",
            Theme = Theme.SkillsForMyFuture,
            BadgeType = BadgeType.InterestBadge,
            Section = Section.Brownie,
            RequiredCompletions = 0
        };

        var result = await _sut.CreateBadgeAsync(badge);

        result.Badge!.RequiredCompletions.Should().Be(3);
    }

    [Fact]
    public async Task GetBadgesByFilterAsync_ShouldFilterByTheme()
    {
        await _sut.CreateBadgeAsync(new BadgeDefinition
        {
            Name = "Badge A", Theme = Theme.BeWell, BadgeType = BadgeType.SkillsBuilder, Section = Section.Brownie
        });
        await _sut.CreateBadgeAsync(new BadgeDefinition
        {
            Name = "Badge B", Theme = Theme.TakeAction, BadgeType = BadgeType.InterestBadge, Section = Section.Brownie
        });

        var result = await _sut.GetBadgesByFilterAsync(theme: Theme.BeWell);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Badge A");
    }

    [Fact]
    public async Task DeleteBadgeAsync_ShouldFail_WhenClausesLinkedToActivities()
    {
        var badgeResult = await _sut.CreateBadgeAsync(new BadgeDefinition
        {
            Name = "Linked Badge",
            Theme = Theme.KnowMyself,
            BadgeType = BadgeType.SkillsBuilder,
            Section = Section.Rainbow,
            Clauses = new List<BadgeClause> { new() { Name = "Clause 1" } }
        });

        // Link clause to a meeting activity
        _context.MeetingActivities.Add(new MeetingActivity
        {
            MeetingId = 1,
            Name = "Test Activity",
            BadgeClauseId = badgeResult.Badge!.Clauses[0].Id
        });
        await _context.SaveChangesAsync();

        var deleteResult = await _sut.DeleteBadgeAsync(badgeResult.Badge.Id);

        deleteResult.Success.Should().BeFalse();
        deleteResult.ErrorMessage.Should().Contain("linked to meeting activities");
    }

    [Fact]
    public async Task CreateUmaAsync_ShouldCreateUma()
    {
        var uma = new UmaDefinition
        {
            Name = "Campfire Songs",
            Theme = Theme.ExpressMyself,
            Minutes = 30,
            Description = "Learn and sing campfire songs"
        };

        var result = await _sut.CreateUmaAsync(uma);

        result.Success.Should().BeTrue();
        result.Uma.Should().NotBeNull();
        result.Uma!.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateUmaAsync_ShouldFail_WhenMinutesZero()
    {
        var uma = new UmaDefinition
        {
            Name = "Bad UMA",
            Theme = Theme.BeWell,
            Minutes = 0
        };

        var result = await _sut.CreateUmaAsync(uma);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SearchClausesAsync_ShouldSearchByName()
    {
        await _sut.CreateBadgeAsync(new BadgeDefinition
        {
            Name = "First Aid",
            Theme = Theme.BeWell,
            BadgeType = BadgeType.SkillsBuilder,
            Section = Section.Guide,
            Clauses = new List<BadgeClause>
            {
                new() { Name = "Learn CPR" },
                new() { Name = "Bandaging skills" }
            }
        });

        var result = await _sut.SearchClausesAsync("CPR");

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Learn CPR");
    }
}

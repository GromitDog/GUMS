using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Tests.Services;

public class ProgrammeServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ProgrammeService _sut;

    public ProgrammeServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _sut = new ProgrammeService(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    private async Task<(BadgeDefinition Badge, Meeting Meeting, Person Girl)> SetupBadgeWithMeeting()
    {
        var girl = new Person
        {
            MembershipNumber = "G001",
            FullName = "Test Girl",
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            DateJoined = DateTime.Today.AddYears(-1),
            IsActive = true
        };
        _context.Persons.Add(girl);

        var badge = new BadgeDefinition
        {
            Name = "First Aid Stage 1",
            Theme = Theme.BeWell,
            BadgeType = BadgeType.SkillsBuilder,
            Section = Section.Brownie,
            RequiredCompletions = 4,
            Clauses = new List<BadgeClause>
            {
                new() { Name = "Clause 1", SortOrder = 0 },
                new() { Name = "Clause 2", SortOrder = 1 },
                new() { Name = "Clause 3", SortOrder = 2 },
                new() { Name = "Clause 4", SortOrder = 3 },
                new() { Name = "Clause 5", SortOrder = 4 }
            }
        };
        _context.BadgeDefinitions.Add(badge);

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

        return (badge, meeting, girl);
    }

    [Fact]
    public async Task BadgeCompletion_ShouldBeComplete_When4Of5ClausesDone()
    {
        var (badge, meeting, girl) = await SetupBadgeWithMeeting();

        // Create meeting activities linked to 4 of the 5 clauses
        for (int i = 0; i < 4; i++)
        {
            var activity = new MeetingActivity
            {
                MeetingId = meeting.Id,
                Name = $"Activity {i + 1}",
                BadgeClauseId = badge.Clauses[i].Id,
                SortOrder = i
            };
            _context.MeetingActivities.Add(activity);
        }
        await _context.SaveChangesAsync();

        // Mark all 4 as completed
        var activities = await _context.MeetingActivities.ToListAsync();
        foreach (var activity in activities)
        {
            _context.ActivityCompletions.Add(new ActivityCompletion
            {
                MeetingActivityId = activity.Id,
                MembershipNumber = "G001",
                Completed = true
            });
        }
        await _context.SaveChangesAsync();

        var progress = await _sut.GetGirlThemeProgressAsync("G001");

        var beWell = progress.First(t => t.Theme == Theme.BeWell);
        beWell.SkillsBuilders.Should().HaveCount(1);
        beWell.SkillsBuilders[0].ClausesCompleted.Should().Be(4);
        beWell.SkillsBuilders[0].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task BadgeCompletion_ShouldNotBeComplete_When3Of5ClausesDone_With4Required()
    {
        var (badge, meeting, girl) = await SetupBadgeWithMeeting();

        // Create meeting activities linked to 3 clauses
        for (int i = 0; i < 3; i++)
        {
            var activity = new MeetingActivity
            {
                MeetingId = meeting.Id,
                Name = $"Activity {i + 1}",
                BadgeClauseId = badge.Clauses[i].Id,
                SortOrder = i
            };
            _context.MeetingActivities.Add(activity);
        }
        await _context.SaveChangesAsync();

        var activities = await _context.MeetingActivities.ToListAsync();
        foreach (var activity in activities)
        {
            _context.ActivityCompletions.Add(new ActivityCompletion
            {
                MeetingActivityId = activity.Id,
                MembershipNumber = "G001",
                Completed = true
            });
        }
        await _context.SaveChangesAsync();

        var progress = await _sut.GetGirlThemeProgressAsync("G001");

        var beWell = progress.First(t => t.Theme == Theme.BeWell);
        beWell.SkillsBuilders[0].ClausesCompleted.Should().Be(3);
        beWell.SkillsBuilders[0].IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task ThemeAward_ShouldBeEarned_WhenAllCriteriaMetForBrownie()
    {
        var girl = new Person
        {
            MembershipNumber = "G002",
            FullName = "Brownie Girl",
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            DateJoined = DateTime.Today.AddYears(-1),
            IsActive = true
        };
        _context.Persons.Add(girl);

        // Create a skills builder with 4 clauses (required: 4)
        var sb = new BadgeDefinition
        {
            Name = "SB Test",
            Theme = Theme.HaveAdventures,
            BadgeType = BadgeType.SkillsBuilder,
            Section = Section.Brownie,
            RequiredCompletions = 4,
            Clauses = Enumerable.Range(1, 4).Select(i => new BadgeClause { Name = $"SB Clause {i}", SortOrder = i }).ToList()
        };

        // Create an interest badge with 3 clauses (required: 3)
        var ib = new BadgeDefinition
        {
            Name = "IB Test",
            Theme = Theme.HaveAdventures,
            BadgeType = BadgeType.InterestBadge,
            Section = Section.Brownie,
            RequiredCompletions = 3,
            Clauses = Enumerable.Range(1, 3).Select(i => new BadgeClause { Name = $"IB Clause {i}", SortOrder = i }).ToList()
        };

        _context.BadgeDefinitions.AddRange(sb, ib);

        // Create UMA (180 min needed for Brownie)
        var uma = new UmaDefinition { Name = "Adventure UMA", Theme = Theme.HaveAdventures, Minutes = 180 };
        _context.UmaDefinitions.Add(uma);

        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Test",
            LocationName = "Hall"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        // Create activities for all clauses and UMA
        var sortOrder = 0;
        foreach (var clause in sb.Clauses)
        {
            _context.MeetingActivities.Add(new MeetingActivity
            {
                MeetingId = meeting.Id, Name = clause.Name, BadgeClauseId = clause.Id, SortOrder = sortOrder++
            });
        }
        foreach (var clause in ib.Clauses)
        {
            _context.MeetingActivities.Add(new MeetingActivity
            {
                MeetingId = meeting.Id, Name = clause.Name, BadgeClauseId = clause.Id, SortOrder = sortOrder++
            });
        }
        _context.MeetingActivities.Add(new MeetingActivity
        {
            MeetingId = meeting.Id, Name = "UMA Activity", UmaDefinitionId = uma.Id, SortOrder = sortOrder
        });
        await _context.SaveChangesAsync();

        // Complete all activities
        var allActivities = await _context.MeetingActivities.ToListAsync();
        foreach (var activity in allActivities)
        {
            _context.ActivityCompletions.Add(new ActivityCompletion
            {
                MeetingActivityId = activity.Id,
                MembershipNumber = "G002",
                Completed = true
            });
        }
        await _context.SaveChangesAsync();

        var progress = await _sut.GetGirlThemeProgressAsync("G002");
        var adventures = progress.First(t => t.Theme == Theme.HaveAdventures);

        adventures.HasCompletedSkillsBuilder.Should().BeTrue();
        adventures.HasCompletedInterestBadge.Should().BeTrue();
        adventures.HasRequiredUmaMinutes.Should().BeTrue();
        adventures.ThemeAwardEarned.Should().BeTrue();
    }

    [Fact]
    public async Task AwardStatus_Bronze_Requires2ThemeAwards()
    {
        var status = new AwardStatus { ThemeAwardsEarned = 2 };
        status.BronzeEarned.Should().BeTrue();
        status.SilverEarned.Should().BeFalse();
    }

    [Fact]
    public async Task AwardStatus_Silver_Requires4ThemeAwards()
    {
        var status = new AwardStatus { ThemeAwardsEarned = 4 };
        status.BronzeEarned.Should().BeTrue();
        status.SilverEarned.Should().BeTrue();
        status.GoldEarned.Should().BeFalse();
    }

    [Fact]
    public async Task AwardStatus_Gold_Requires6ThemeAwards_PlusChallenge()
    {
        var status = new AwardStatus { ThemeAwardsEarned = 6, GoldChallengeComplete = false };
        status.GoldEarned.Should().BeFalse();

        status.GoldChallengeComplete = true;
        status.GoldEarned.Should().BeTrue();
    }

    [Fact]
    public async Task UmaMinutesRequired_ShouldVaryBySection()
    {
        // Rainbow: 120, Brownie: 180, Guide: 240, Ranger: 240
        foreach (var (sec, expected) in new[] {
            (Section.Rainbow, 120),
            (Section.Brownie, 180),
            (Section.Guide, 240),
            (Section.Ranger, 240)
        })
        {
            var girl = new Person
            {
                MembershipNumber = $"SEC-{sec}",
                FullName = $"{sec} Girl",
                PersonType = PersonType.Girl,
                Section = sec,
                DateJoined = DateTime.Today.AddYears(-1),
                IsActive = true
            };
            _context.Persons.Add(girl);
        }
        await _context.SaveChangesAsync();

        var rainbowProgress = await _sut.GetGirlThemeProgressAsync("SEC-Rainbow");
        rainbowProgress[0].UmaMinutesRequired.Should().Be(120);

        var brownieProgress = await _sut.GetGirlThemeProgressAsync("SEC-Brownie");
        brownieProgress[0].UmaMinutesRequired.Should().Be(180);

        var guideProgress = await _sut.GetGirlThemeProgressAsync("SEC-Guide");
        guideProgress[0].UmaMinutesRequired.Should().Be(240);
    }

    [Fact]
    public async Task SaveCompletionsAsync_ShouldSaveAndUpdate()
    {
        var meeting = new Meeting
        {
            Date = DateTime.Today,
            StartTime = new TimeOnly(18, 30),
            EndTime = new TimeOnly(19, 30),
            MeetingType = MeetingType.Regular,
            Title = "Test",
            LocationName = "Hall"
        };
        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        var activity = new MeetingActivity
        {
            MeetingId = meeting.Id,
            Name = "Activity 1",
            SortOrder = 0
        };
        _context.MeetingActivities.Add(activity);
        await _context.SaveChangesAsync();

        // Save completions
        var completions = new List<CompletionRecord>
        {
            new() { MeetingActivityId = activity.Id, MembershipNumber = "G001", Completed = true },
            new() { MeetingActivityId = activity.Id, MembershipNumber = "G002", Completed = false }
        };

        var result = await _sut.SaveCompletionsAsync(meeting.Id, completions);
        result.Success.Should().BeTrue();
        result.RecordsSaved.Should().Be(2);

        // Update a completion
        completions[1].Completed = true;
        var result2 = await _sut.SaveCompletionsAsync(meeting.Id, completions);
        result2.Success.Should().BeTrue();

        var saved = await _context.ActivityCompletions.ToListAsync();
        saved.Should().HaveCount(2);
        saved.Should().AllSatisfy(c => c.Completed.Should().BeTrue());
    }

    [Fact]
    public async Task SetGoldChallengeCompleteAsync_ShouldCreateAndUpdate()
    {
        var result = await _sut.SetGoldChallengeCompleteAsync("G001", true);
        result.Success.Should().BeTrue();

        var tracking = await _context.AwardTrackings.FirstAsync(a => a.MembershipNumber == "G001");
        tracking.GoldChallengeComplete.Should().BeTrue();
        tracking.GoldChallengeDate.Should().NotBeNull();

        // Toggle off
        await _sut.SetGoldChallengeCompleteAsync("G001", false);
        tracking = await _context.AwardTrackings.FirstAsync(a => a.MembershipNumber == "G001");
        tracking.GoldChallengeComplete.Should().BeFalse();
    }
}

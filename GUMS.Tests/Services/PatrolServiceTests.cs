using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GUMS.Tests.Services;

public class PatrolServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PatrolService _sut;

    public PatrolServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _sut = new PatrolService(_context);
    }

    public void Dispose() => _context?.Dispose();

    private async Task<Person> AddGirl(string membershipNumber, string name, Section section)
    {
        var girl = new Person
        {
            MembershipNumber = membershipNumber,
            FullName = name,
            PersonType = PersonType.Girl,
            Section = section,
            DateJoined = DateTime.Today.AddYears(-1),
            IsActive = true
        };
        _context.Persons.Add(girl);
        await _context.SaveChangesAsync();
        return girl;
    }

    [Fact]
    public async Task EnsureDefaultPatrolBadgesAsync_SeedsFourRoleBadges_Idempotent()
    {
        await _sut.EnsureDefaultPatrolBadgesAsync();
        await _sut.EnsureDefaultPatrolBadgesAsync();

        var patrolBadges = await _context.BadgeDefinitions
            .Where(b => b.BadgeType == BadgeType.PatrolBadge)
            .ToListAsync();

        patrolBadges.Should().HaveCount(4);
        patrolBadges.Select(b => b.Name).Should().BeEquivalentTo(new[]
        {
            "Sixer", "Brownie Seconder", "Patrol Leader", "Guide Seconder"
        });
    }

    [Fact]
    public async Task CreatePatrolAsync_CreatesPatrolAndEmblemBadge()
    {
        var result = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);

        result.Success.Should().BeTrue();
        result.Patrol.Should().NotBeNull();

        var emblem = await _context.BadgeDefinitions
            .FirstAsync(b => b.Id == result.Patrol!.EmblemBadgeDefinitionId);
        emblem.Name.Should().Be("Rabbit Emblem");
        emblem.BadgeType.Should().Be(BadgeType.PatrolBadge);
        emblem.Section.Should().Be(Section.Brownie);
    }

    [Fact]
    public async Task CreatePatrolAsync_RejectsDuplicateWithinSection_AllowsAcrossSections()
    {
        await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);

        var dupe = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        dupe.Success.Should().BeFalse();

        var otherSection = await _sut.CreatePatrolAsync("Rabbit", Section.Guide);
        otherSection.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePatrolAsync_RejectsNonBrownieGuideSection()
    {
        var result = await _sut.CreatePatrolAsync("Rainbows", Section.Rainbow);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RenamePatrolAsync_UpdatesPatrolAndEmblemBadge()
    {
        var created = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);

        var result = await _sut.RenamePatrolAsync(created.Patrol!.Id, "Hedgehog");
        result.Success.Should().BeTrue();

        var patrol = await _context.Patrols.FindAsync(created.Patrol.Id);
        patrol!.Name.Should().Be("Hedgehog");

        var emblem = await _context.BadgeDefinitions.FindAsync(patrol.EmblemBadgeDefinitionId);
        emblem!.Name.Should().Be("Hedgehog Emblem");
    }

    [Fact]
    public async Task DeletePatrolAsync_RejectsWhenMembersPresent()
    {
        var created = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var girl = await AddGirl("G001", "Alice", Section.Brownie);
        await _sut.AssignGirlToPatrolAsync(girl.MembershipNumber, created.Patrol!.Id);

        var result = await _sut.DeletePatrolAsync(created.Patrol.Id);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePatrolAsync_DeletesEmblem_WhenNoAwardsExist()
    {
        var created = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var emblemId = created.Patrol!.EmblemBadgeDefinitionId;

        var result = await _sut.DeletePatrolAsync(created.Patrol.Id);
        result.Success.Should().BeTrue();

        (await _context.Patrols.FindAsync(created.Patrol.Id)).Should().BeNull();
        (await _context.BadgeDefinitions.FindAsync(emblemId)).Should().BeNull();
    }

    [Fact]
    public async Task DeletePatrolAsync_KeepsEmblem_DeactivatesStock_WhenAwardsExist()
    {
        var created = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var emblemId = created.Patrol!.EmblemBadgeDefinitionId;

        _context.AwardedBadges.Add(new AwardedBadge
        {
            MembershipNumber = "G001",
            BadgeDefinitionId = emblemId,
            DateAwarded = DateTime.Today
        });
        _context.BadgeStockItems.Add(new BadgeStockItem
        {
            Name = "Rabbit Emblem",
            StockType = BadgeStockType.InterestBadge,
            BadgeDefinitionId = emblemId,
            CurrentQuantity = 5,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var result = await _sut.DeletePatrolAsync(created.Patrol.Id);
        result.Success.Should().BeTrue();

        (await _context.BadgeDefinitions.FindAsync(emblemId)).Should().NotBeNull();
        var stock = await _context.BadgeStockItems
            .FirstAsync(s => s.BadgeDefinitionId == emblemId);
        stock.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AssignGirlToPatrolAsync_RejectsSectionMismatch()
    {
        var created = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var guide = await AddGirl("G001", "Alice", Section.Guide);

        var result = await _sut.AssignGirlToPatrolAsync(guide.MembershipNumber, created.Patrol!.Id);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AssignGirlToPatrolAsync_ResetsRoleOnReassignment()
    {
        var rabbit = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var hedgehog = await _sut.CreatePatrolAsync("Hedgehog", Section.Brownie);
        var girl = await AddGirl("G001", "Alice", Section.Brownie);

        await _sut.AssignGirlToPatrolAsync(girl.MembershipNumber, rabbit.Patrol!.Id);
        await _sut.SetRoleAsync(girl.MembershipNumber, PatrolRole.Leader);

        var moved = await _sut.AssignGirlToPatrolAsync(girl.MembershipNumber, hedgehog.Patrol!.Id);
        moved.Success.Should().BeTrue();

        var refreshed = await _context.Persons.FirstAsync(p => p.MembershipNumber == "G001");
        refreshed.PatrolRole.Should().Be(PatrolRole.Member);
        refreshed.PatrolId.Should().Be(hedgehog.Patrol.Id);
    }

    [Fact]
    public async Task SetRoleAsync_DemotesExistingLeaderOfSamePatrol()
    {
        var patrol = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var alice = await AddGirl("G001", "Alice", Section.Brownie);
        var bella = await AddGirl("G002", "Bella", Section.Brownie);

        await _sut.AssignGirlToPatrolAsync(alice.MembershipNumber, patrol.Patrol!.Id);
        await _sut.AssignGirlToPatrolAsync(bella.MembershipNumber, patrol.Patrol.Id);

        await _sut.SetRoleAsync(alice.MembershipNumber, PatrolRole.Leader);
        await _sut.SetRoleAsync(bella.MembershipNumber, PatrolRole.Leader);

        var aliceNow = await _context.Persons.FirstAsync(p => p.MembershipNumber == "G001");
        var bellaNow = await _context.Persons.FirstAsync(p => p.MembershipNumber == "G002");

        aliceNow.PatrolRole.Should().Be(PatrolRole.Member);
        bellaNow.PatrolRole.Should().Be(PatrolRole.Leader);
    }

    [Fact]
    public async Task SetRoleAsync_FailsWhenNoPatrolAssigned()
    {
        var girl = await AddGirl("G001", "Alice", Section.Brownie);

        var result = await _sut.SetRoleAsync(girl.MembershipNumber, PatrolRole.Leader);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetPatrolAwardsDueAsync_IncludesEmblemAndRoleBadges_ExcludesAwarded()
    {
        await _sut.EnsureDefaultPatrolBadgesAsync();
        var patrol = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);

        var alice = await AddGirl("G001", "Alice", Section.Brownie);
        var bella = await AddGirl("G002", "Bella", Section.Brownie);
        var cara = await AddGirl("G003", "Cara", Section.Brownie);

        await _sut.AssignGirlToPatrolAsync(alice.MembershipNumber, patrol.Patrol!.Id);
        await _sut.AssignGirlToPatrolAsync(bella.MembershipNumber, patrol.Patrol.Id);
        await _sut.AssignGirlToPatrolAsync(cara.MembershipNumber, patrol.Patrol.Id);

        await _sut.SetRoleAsync(alice.MembershipNumber, PatrolRole.Leader);

        // Cara already has her emblem — shouldn't appear for emblem.
        _context.AwardedBadges.Add(new AwardedBadge
        {
            MembershipNumber = cara.MembershipNumber,
            BadgeDefinitionId = patrol.Patrol.EmblemBadgeDefinitionId,
            DateAwarded = DateTime.Today
        });
        await _context.SaveChangesAsync();

        var due = await _sut.GetPatrolAwardsDueAsync();

        var emblemAwards = due.Where(a => a.AwardName == "Rabbit Emblem").ToList();
        emblemAwards.Select(a => a.MembershipNumber).Should().BeEquivalentTo(new[] { "G001", "G002" });

        var leaderAwards = due.Where(a => a.AwardName == "Sixer").ToList();
        leaderAwards.Should().ContainSingle();
        leaderAwards[0].MembershipNumber.Should().Be("G001");
        leaderAwards[0].AwardType.Should().Be("PatrolBadge");
    }

    [Fact]
    public async Task GetPatrolAwardsDueAsync_RoleBadgeDisappearsOnceAwarded()
    {
        await _sut.EnsureDefaultPatrolBadgesAsync();
        var patrol = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var alice = await AddGirl("G001", "Alice", Section.Brownie);
        await _sut.AssignGirlToPatrolAsync(alice.MembershipNumber, patrol.Patrol!.Id);
        await _sut.SetRoleAsync(alice.MembershipNumber, PatrolRole.Leader);

        var sixerId = await _sut.GetRoleBadgeDefinitionIdAsync(Section.Brownie, PatrolRole.Leader);
        _context.AwardedBadges.Add(new AwardedBadge
        {
            MembershipNumber = alice.MembershipNumber,
            BadgeDefinitionId = sixerId,
            DateAwarded = DateTime.Today
        });
        await _context.SaveChangesAsync();

        var due = await _sut.GetPatrolAwardsDueAsync();
        due.Should().NotContain(a => a.AwardName == "Sixer");
    }

    [Fact]
    public async Task MarkBadgeAwardedAsync_ForEmblem_InvokesInventoryDecrement()
    {
        var inventory = new Mock<IInventoryService>();
        var programme = new ProgrammeService(_context, inventory.Object);

        var patrol = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var alice = await AddGirl("G001", "Alice", Section.Brownie);
        await _sut.AssignGirlToPatrolAsync(alice.MembershipNumber, patrol.Patrol!.Id);

        await programme.MarkBadgeAwardedAsync(alice.MembershipNumber, patrol.Patrol.EmblemBadgeDefinitionId);

        inventory.Verify(
            i => i.TryDecrementForBadgeAsync(patrol.Patrol.EmblemBadgeDefinitionId, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveGirlFromPatrolAsync_ClearsPatrolAndRole()
    {
        var patrol = await _sut.CreatePatrolAsync("Rabbit", Section.Brownie);
        var alice = await AddGirl("G001", "Alice", Section.Brownie);
        await _sut.AssignGirlToPatrolAsync(alice.MembershipNumber, patrol.Patrol!.Id);
        await _sut.SetRoleAsync(alice.MembershipNumber, PatrolRole.Leader);

        var result = await _sut.RemoveGirlFromPatrolAsync(alice.MembershipNumber);
        result.Success.Should().BeTrue();

        var refreshed = await _context.Persons.FirstAsync(p => p.MembershipNumber == "G001");
        refreshed.PatrolId.Should().BeNull();
        refreshed.PatrolRole.Should().Be(PatrolRole.Member);
    }
}

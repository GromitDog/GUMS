using GUMS.Data.Enums;

namespace GUMS.Services;

public interface IProgrammeService
{
    // ===== Activity Completions =====
    Task<(bool Success, string ErrorMessage, int RecordsSaved)> SaveCompletionsAsync(
        int meetingId, List<CompletionRecord> completions);
    Task<List<CompletionRecord>> GetCompletionsForMeetingAsync(int meetingId);

    // ===== Girl Progress =====
    Task<GirlProgress> GetGirlProgressAsync(string membershipNumber);
    Task<List<GirlThemeProgress>> GetGirlThemeProgressAsync(string membershipNumber);
    Task<AwardStatus> GetAwardStatusAsync(string membershipNumber);

    // ===== Unit Overview =====
    Task<UnitOverview> GetUnitOverviewAsync(Section? section = null);

    // ===== Awards =====
    Task<List<AwardDue>> GetAwardsDueAsync();
    Task<(bool Success, string ErrorMessage)> MarkBadgeAwardedAsync(string membershipNumber, int badgeDefinitionId);
    Task<(bool Success, string ErrorMessage)> UnmarkBadgeAwardedAsync(string membershipNumber, int badgeDefinitionId);

    /// <summary>Badges awarded in the last `days` days across all girls, newest first.</summary>
    Task<List<AwardedBadgeSummary>> GetRecentAwardsAsync(int days = 30);

    /// <summary>All awarded badges for a specific girl, newest first.</summary>
    Task<List<AwardedBadgeSummary>> GetAwardedBadgesForGirlAsync(string membershipNumber);
    Task<(bool Success, string ErrorMessage)> SetGoldChallengeCompleteAsync(string membershipNumber, bool complete);
    Task<(bool Success, string ErrorMessage)> MarkThemeAwardedAsync(string membershipNumber, Theme theme);
    Task<(bool Success, string ErrorMessage)> MarkLevelAwardedAsync(string membershipNumber, string level);
    Task<(bool Success, string ErrorMessage)> MarkNightsAwayBadgeAwardedAsync(string membershipNumber, int milestone);
    Task<Dictionary<string, HashSet<int>>> GetAwardedNightsAwayMilestonesAsync(List<string> membershipNumbers);

    // ===== Standalone Completions =====
    Task<(bool Success, string ErrorMessage)> SaveStandaloneCompletionAsync(
        string membershipNumber, int? badgeClauseId, int? umaDefinitionId, bool completed);
    Task<List<StandaloneCompletionDto>> GetStandaloneCompletionsAsync(string membershipNumber);
    Task<HashSet<int>> GetAllCompletedUmaIdsAsync(string membershipNumber);
    Task<HashSet<int>> GetStandaloneCompletedUmaIdsAsync(string membershipNumber);

    // ===== Stock Forecast =====
    /// <summary>
    /// Returns badges that would be newly completed by end of the current term
    /// if all active members attended all remaining planned meetings,
    /// excluding badges already in the currently-due list.
    /// </summary>
    Task<List<AwardDue>> GetProjectedAwardsForCurrentTermAsync();

    // ===== Term Balance =====
    Task<TermBalance> GetTermBalanceAsync(int termId);

    // ===== One-off Backfills =====
    /// <summary>
    /// Creates ActivityCompletion rows for fun-badge activities attached to
    /// past meetings that never received completions (the RecordAttendance UI
    /// did not expose them until a later release). For every past meeting with
    /// a fun-badge activity and no existing completions, every girl marked
    /// Attended on that meeting is recorded as having completed it.
    /// Idempotent — the !Completions.Any() guard makes subsequent runs no-ops.
    /// </summary>
    /// <returns>The number of ActivityCompletion rows created.</returns>
    Task<int> BackfillFunBadgeCompletionsAsync();
}

// ===== DTOs =====

public class CompletionRecord
{
    public int MeetingActivityId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public bool Completed { get; set; }
}

public class GirlProgress
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Section? Section { get; set; }
    public List<GirlThemeProgress> ThemeProgress { get; set; } = new();
    public AwardStatus Awards { get; set; } = new();
}

public class GirlThemeProgress
{
    public Theme Theme { get; set; }
    public string ThemeDisplayName { get; set; } = string.Empty;

    // Skills Builder progress
    public List<BadgeProgress> SkillsBuilders { get; set; } = new();
    public bool HasCompletedSkillsBuilder => SkillsBuilders.Any(b => b.IsComplete);

    // Interest Badge progress
    public List<BadgeProgress> InterestBadges { get; set; } = new();
    public bool HasCompletedInterestBadge => InterestBadges.Any(b => b.IsComplete);

    // UMA hours
    public int UmaMinutesCompleted { get; set; }
    public int UmaMinutesRequired { get; set; }
    public bool HasRequiredUmaMinutes => UmaMinutesCompleted >= UmaMinutesRequired;

    // Theme Award
    public bool ThemeAwardEarned => HasCompletedSkillsBuilder && HasCompletedInterestBadge && HasRequiredUmaMinutes;
}

public class BadgeProgress
{
    public int BadgeDefinitionId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public int ClausesCompleted { get; set; }
    public int ClausesTotal { get; set; }
    public int RequiredCompletions { get; set; }
    public bool IsComplete => ClausesCompleted >= RequiredCompletions;
    public bool IsAwarded { get; set; }
    public List<ClauseProgress> Clauses { get; set; } = new();
}

public class ClauseProgress
{
    public int BadgeClauseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Completed { get; set; }
}

public class StandaloneCompletionDto
{
    public int? BadgeClauseId { get; set; }
    public int? UmaDefinitionId { get; set; }
}

public class AwardStatus
{
    public int ThemeAwardsEarned { get; set; }
    public bool BronzeEarned => ThemeAwardsEarned >= 2;
    public bool SilverEarned => ThemeAwardsEarned >= 4;
    public bool GoldEarned => ThemeAwardsEarned >= 6 && GoldChallengeComplete;
    public bool GoldChallengeComplete { get; set; }
}

public class UnitOverview
{
    public List<GirlOverviewRow> Girls { get; set; } = new();
    public Dictionary<Theme, int> ThemeCoverage { get; set; } = new();
}

public class GirlOverviewRow
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Section? Section { get; set; }
    public DateTime DateJoined { get; set; }
    public Dictionary<Theme, ThemeProgressSummary> Themes { get; set; } = new();
    public int ThemeAwardsEarned { get; set; }
    public List<string> CompletedFunBadges { get; set; } = new();
}

public class ThemeProgressSummary
{
    public ThemeStatus Status { get; set; }
    public int SkillsBuildersComplete { get; set; }
    public bool SkillsBuilderStarted { get; set; }
    /// <summary>Best skills builder progress as percentage (0-100)</summary>
    public int SkillsBuilderPercent { get; set; }
    public int InterestBadgesComplete { get; set; }
    public bool InterestBadgeStarted { get; set; }
    /// <summary>Best interest badge progress as percentage (0-100)</summary>
    public int InterestBadgePercent { get; set; }
    public int UmaMinutes { get; set; }
    public int UmaMinutesRequired { get; set; }
}

public enum ThemeStatus
{
    NotStarted,
    InProgress,
    ThemeAwardEarned
}

public class AwardDue
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string AwardName { get; set; } = string.Empty;
    public string AwardType { get; set; } = string.Empty; // "Badge", "ThemeAward", "Bronze", "Silver", "Gold", "NightsAway"
    public int? BadgeDefinitionId { get; set; }
    public int? Milestone { get; set; }
    public Theme? Theme { get; set; }
    public DateTime? CompletedDate { get; set; }
}

public class AwardedBadgeSummary
{
    public int AwardedBadgeId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string? GirlName { get; set; }
    public int BadgeDefinitionId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public BadgeType BadgeType { get; set; }
    public DateTime DateAwarded { get; set; }
}

public class TermBalance
{
    public int TermId { get; set; }
    public string? TermName { get; set; }
    public Dictionary<Theme, ThemeBalance> ThemeBalances { get; set; } = new();
    public int TotalMinutesPlanned { get; set; }
    public int TotalUmaMinutesPlanned { get; set; }
    public int TotalBadgesWorkedOn { get; set; }
    public int FunBadgesWorkedOn { get; set; }
    public int NightsAwayOffered { get; set; }
}

public class ThemeBalance
{
    public Theme Theme { get; set; }
    public int MinutesPlanned { get; set; }
    public int UmaMinutesPlanned { get; set; }
    public int BadgesWorkedOn { get; set; }
    public int SkillsBuildersWorkedOn { get; set; }
    public int InterestBadgesWorkedOn { get; set; }
    public double PercentageOfTotal { get; set; }
}

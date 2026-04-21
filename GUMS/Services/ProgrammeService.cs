using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class ProgrammeService : IProgrammeService
{
    public static readonly int[] NightsAwayMilestones = { 1, 5, 10, 15, 20, 25, 30 };

    private readonly ApplicationDbContext _context;
    private readonly IInventoryService _inventory;

    public ProgrammeService(ApplicationDbContext context, IInventoryService inventory)
    {
        _context = context;
        _inventory = inventory;
    }

    // ===== Activity Completions =====

    public async Task<(bool Success, string ErrorMessage, int RecordsSaved)> SaveCompletionsAsync(
        int meetingId, List<CompletionRecord> completions)
    {
        if (completions == null || !completions.Any())
            return (false, "No completion records provided.", 0);

        var activityIds = completions.Select(c => c.MeetingActivityId).Distinct().ToList();
        var validActivities = await _context.MeetingActivities
            .Where(a => a.MeetingId == meetingId && activityIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync();

        var existing = await _context.ActivityCompletions
            .Where(c => validActivities.Contains(c.MeetingActivityId))
            .ToDictionaryAsync(c => (c.MeetingActivityId, c.MembershipNumber));

        var saved = 0;
        foreach (var record in completions)
        {
            if (!validActivities.Contains(record.MeetingActivityId))
                continue;

            var key = (record.MeetingActivityId, record.MembershipNumber);
            if (existing.TryGetValue(key, out var ex))
            {
                ex.Completed = record.Completed;
            }
            else
            {
                _context.ActivityCompletions.Add(new ActivityCompletion
                {
                    MeetingActivityId = record.MeetingActivityId,
                    MembershipNumber = record.MembershipNumber,
                    Completed = record.Completed
                });
            }
            saved++;
        }

        if (saved > 0)
            await _context.SaveChangesAsync();

        return (true, string.Empty, saved);
    }

    public async Task<List<CompletionRecord>> GetCompletionsForMeetingAsync(int meetingId)
    {
        var activityIds = await _context.MeetingActivities
            .Where(a => a.MeetingId == meetingId)
            .Select(a => a.Id)
            .ToListAsync();

        return await _context.ActivityCompletions
            .AsNoTracking()
            .Where(c => activityIds.Contains(c.MeetingActivityId))
            .Select(c => new CompletionRecord
            {
                MeetingActivityId = c.MeetingActivityId,
                MembershipNumber = c.MembershipNumber,
                Completed = c.Completed
            })
            .ToListAsync();
    }

    // ===== Girl Progress =====

    public async Task<GirlProgress> GetGirlProgressAsync(string membershipNumber)
    {
        var person = await _context.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber && !p.IsDataRemoved);

        var progress = new GirlProgress
        {
            MembershipNumber = membershipNumber,
            Name = person?.FullName,
            Section = person?.Section,
            ThemeProgress = await GetGirlThemeProgressAsync(membershipNumber)
        };

        progress.Awards = CalculateAwardStatus(progress.ThemeProgress, membershipNumber);

        // Check gold challenge
        var tracking = await _context.AwardTrackings
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.MembershipNumber == membershipNumber);
        progress.Awards.GoldChallengeComplete = tracking?.GoldChallengeComplete ?? false;

        return progress;
    }

    public async Task<List<GirlThemeProgress>> GetGirlThemeProgressAsync(string membershipNumber)
    {
        var person = await _context.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);

        var section = person?.Section;
        var umaMinutesRequired = GetUmaMinutesRequired(section);

        // Get all completed activity IDs for this girl
        var completedActivityIds = await _context.ActivityCompletions
            .AsNoTracking()
            .Where(c => c.MembershipNumber == membershipNumber && c.Completed)
            .Select(c => c.MeetingActivityId)
            .ToListAsync();

        // Get badge clause completions
        var completedBadgeClauses = await _context.MeetingActivities
            .AsNoTracking()
            .Where(a => completedActivityIds.Contains(a.Id) && a.BadgeClauseId.HasValue)
            .Select(a => a.BadgeClauseId!.Value)
            .Distinct()
            .ToListAsync();

        // Get UMA completions
        var completedUmaActivities = await _context.MeetingActivities
            .Include(a => a.UmaDefinition)
            .AsNoTracking()
            .Where(a => completedActivityIds.Contains(a.Id) && a.UmaDefinitionId.HasValue)
            .ToListAsync();

        // Get awarded badges for this girl
        var awardedBadgeIds = await _context.AwardedBadges
            .AsNoTracking()
            .Where(a => a.MembershipNumber == membershipNumber)
            .Select(a => a.BadgeDefinitionId)
            .ToHashSetAsync();

        // Get all badge definitions with clauses
        var allBadges = await _context.BadgeDefinitions
            .Include(b => b.Clauses)
            .AsNoTracking()
            .ToListAsync();

        // Get awarded badges
        // (We'll track this via a simple approach: check if badge is complete)

        var themes = Enum.GetValues<Theme>();
        var result = new List<GirlThemeProgress>();

        foreach (var theme in themes)
        {
            var themeProgress = new GirlThemeProgress
            {
                Theme = theme,
                ThemeDisplayName = GetThemeDisplayName(theme),
                UmaMinutesRequired = umaMinutesRequired
            };

            // Skills Builders for this theme
            var skillsBuilders = allBadges
                .Where(b => b.Theme == theme && b.BadgeType == BadgeType.SkillsBuilder)
                .ToList();

            foreach (var sb in skillsBuilders)
            {
                var clauseIds = sb.Clauses.Select(c => c.Id).ToList();
                var completed = clauseIds.Count(id => completedBadgeClauses.Contains(id));

                themeProgress.SkillsBuilders.Add(new BadgeProgress
                {
                    BadgeDefinitionId = sb.Id,
                    BadgeName = sb.Name,
                    ClausesCompleted = completed,
                    ClausesTotal = clauseIds.Count,
                    RequiredCompletions = sb.RequiredCompletions,
                    IsAwarded = awardedBadgeIds.Contains(sb.Id),
                    Clauses = sb.Clauses.OrderBy(c => c.SortOrder).Select(c => new ClauseProgress
                    {
                        BadgeClauseId = c.Id,
                        Name = c.Name,
                        Completed = completedBadgeClauses.Contains(c.Id)
                    }).ToList()
                });
            }

            // Interest Badges for this theme
            var interestBadges = allBadges
                .Where(b => b.Theme == theme && b.BadgeType == BadgeType.InterestBadge)
                .ToList();

            foreach (var ib in interestBadges)
            {
                var clauseIds = ib.Clauses.Select(c => c.Id).ToList();
                var completed = clauseIds.Count(id => completedBadgeClauses.Contains(id));

                themeProgress.InterestBadges.Add(new BadgeProgress
                {
                    BadgeDefinitionId = ib.Id,
                    BadgeName = ib.Name,
                    ClausesCompleted = completed,
                    ClausesTotal = clauseIds.Count,
                    RequiredCompletions = ib.RequiredCompletions,
                    IsAwarded = awardedBadgeIds.Contains(ib.Id),
                    Clauses = ib.Clauses.OrderBy(c => c.SortOrder).Select(c => new ClauseProgress
                    {
                        BadgeClauseId = c.Id,
                        Name = c.Name,
                        Completed = completedBadgeClauses.Contains(c.Id)
                    }).ToList()
                });
            }

            // UMA minutes for this theme
            themeProgress.UmaMinutesCompleted = completedUmaActivities
                .Where(a => a.UmaDefinition?.Theme == theme)
                .Sum(a => a.UmaDefinition?.Minutes ?? 0);

            result.Add(themeProgress);
        }

        return result;
    }

    public async Task<AwardStatus> GetAwardStatusAsync(string membershipNumber)
    {
        var themeProgress = await GetGirlThemeProgressAsync(membershipNumber);
        var status = CalculateAwardStatus(themeProgress, membershipNumber);

        var tracking = await _context.AwardTrackings
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.MembershipNumber == membershipNumber);
        status.GoldChallengeComplete = tracking?.GoldChallengeComplete ?? false;

        return status;
    }

    // ===== Unit Overview =====

    public async Task<UnitOverview> GetUnitOverviewAsync(Section? section = null)
    {
        var query = _context.Persons
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDataRemoved && p.PersonType == PersonType.Girl);

        if (section.HasValue)
            query = query.Where(p => p.Section == section.Value);

        var girls = await query.OrderBy(p => p.FullName).ToListAsync();

        var overview = new UnitOverview();
        var themes = Enum.GetValues<Theme>();

        foreach (var girl in girls)
        {
            var themeProgress = await GetGirlThemeProgressAsync(girl.MembershipNumber);
            var row = new GirlOverviewRow
            {
                MembershipNumber = girl.MembershipNumber,
                Name = girl.FullName,
                Section = girl.Section,
                DateJoined = girl.DateJoined
            };

            // Fun badges completed by this girl (awarded or completed via activities)
            var awardedFunBadgeNames = await _context.AwardedBadges
                .AsNoTracking()
                .Where(a => a.MembershipNumber == girl.MembershipNumber
                    && a.BadgeDefinition.BadgeType == BadgeType.FunBadge)
                .Select(a => a.BadgeDefinition.Name)
                .ToListAsync();

            var completedFunBadgeNames = await _context.ActivityCompletions
                .AsNoTracking()
                .Where(c => c.MembershipNumber == girl.MembershipNumber && c.Completed
                    && c.MeetingActivity.BadgeDefinitionId.HasValue
                    && c.MeetingActivity.BadgeDefinition!.BadgeType == BadgeType.FunBadge)
                .Select(c => c.MeetingActivity.BadgeDefinition!.Name)
                .Distinct()
                .ToListAsync();

            row.CompletedFunBadges = awardedFunBadgeNames
                .Union(completedFunBadgeNames)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            foreach (var theme in themes)
            {
                var tp = themeProgress.FirstOrDefault(t => t.Theme == theme);
                var summary = new ThemeProgressSummary();

                if (tp == null)
                {
                    summary.Status = ThemeStatus.NotStarted;
                }
                else
                {
                    summary.SkillsBuildersComplete = tp.SkillsBuilders.Count(b => b.IsComplete);
                    summary.SkillsBuilderStarted = tp.SkillsBuilders.Any(b => b.ClausesCompleted > 0);
                    if (tp.SkillsBuilders.Any())
                        summary.SkillsBuilderPercent = Math.Min(tp.SkillsBuilders.Max(b => b.RequiredCompletions > 0 ? b.ClausesCompleted * 100 / b.RequiredCompletions : 0), 100);

                    summary.InterestBadgesComplete = tp.InterestBadges.Count(b => b.IsComplete);
                    summary.InterestBadgeStarted = tp.InterestBadges.Any(b => b.ClausesCompleted > 0);
                    if (tp.InterestBadges.Any())
                        summary.InterestBadgePercent = Math.Min(tp.InterestBadges.Max(b => b.RequiredCompletions > 0 ? b.ClausesCompleted * 100 / b.RequiredCompletions : 0), 100);

                    summary.UmaMinutes = tp.UmaMinutesCompleted;
                    summary.UmaMinutesRequired = tp.UmaMinutesRequired;

                    if (tp.ThemeAwardEarned)
                    {
                        summary.Status = ThemeStatus.ThemeAwardEarned;
                        row.ThemeAwardsEarned++;
                    }
                    else if (summary.SkillsBuilderStarted || summary.InterestBadgeStarted || summary.UmaMinutes > 0)
                    {
                        summary.Status = ThemeStatus.InProgress;
                    }
                    else
                    {
                        summary.Status = ThemeStatus.NotStarted;
                    }
                }

                row.Themes[theme] = summary;
            }

            overview.Girls.Add(row);
        }

        // Theme coverage: count of girls with at least InProgress per theme
        foreach (var theme in themes)
        {
            overview.ThemeCoverage[theme] = overview.Girls
                .Count(g => g.Themes.ContainsKey(theme) && g.Themes[theme].Status == ThemeStatus.ThemeAwardEarned);
        }

        return overview;
    }

    // ===== Awards =====

    public async Task<List<AwardDue>> GetAwardsDueAsync()
    {
        var girls = await _context.Persons
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDataRemoved && p.PersonType == PersonType.Girl)
            .ToListAsync();

        var awards = new List<AwardDue>();

        foreach (var girl in girls)
        {
            var progress = await GetGirlProgressAsync(girl.MembershipNumber);

            // Load awarded themes for this girl
            var awardedThemes = await _context.AwardedThemeAwards
                .AsNoTracking()
                .Where(a => a.MembershipNumber == girl.MembershipNumber)
                .Select(a => a.Theme)
                .ToHashSetAsync();

            // Load tracking record for level award filtering
            var tracking = await _context.AwardTrackings
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MembershipNumber == girl.MembershipNumber);

            // Check completed badges
            foreach (var theme in progress.ThemeProgress)
            {
                foreach (var badge in theme.SkillsBuilders.Concat(theme.InterestBadges))
                {
                    if (badge.IsComplete && !badge.IsAwarded)
                    {
                        awards.Add(new AwardDue
                        {
                            MembershipNumber = girl.MembershipNumber,
                            Name = girl.FullName,
                            AwardName = badge.BadgeName,
                            AwardType = "Badge",
                            BadgeDefinitionId = badge.BadgeDefinitionId
                        });
                    }
                }

                if (theme.ThemeAwardEarned && !awardedThemes.Contains(theme.Theme))
                {
                    awards.Add(new AwardDue
                    {
                        MembershipNumber = girl.MembershipNumber,
                        Name = girl.FullName,
                        AwardName = $"{theme.ThemeDisplayName} Theme Award",
                        AwardType = "ThemeAward",
                        Theme = theme.Theme
                    });
                }
            }

            if (progress.Awards.BronzeEarned && !(tracking?.BronzeAwardedDate.HasValue ?? false))
            {
                awards.Add(new AwardDue
                {
                    MembershipNumber = girl.MembershipNumber,
                    Name = girl.FullName,
                    AwardName = "Bronze Award",
                    AwardType = "Bronze"
                });
            }

            if (progress.Awards.SilverEarned && !(tracking?.SilverAwardedDate.HasValue ?? false))
            {
                awards.Add(new AwardDue
                {
                    MembershipNumber = girl.MembershipNumber,
                    Name = girl.FullName,
                    AwardName = "Silver Award",
                    AwardType = "Silver"
                });
            }

            if (progress.Awards.GoldEarned && !(tracking?.GoldAwardedDate.HasValue ?? false))
            {
                awards.Add(new AwardDue
                {
                    MembershipNumber = girl.MembershipNumber,
                    Name = girl.FullName,
                    AwardName = "Gold Award",
                    AwardType = "Gold"
                });
            }

            // Check fun badges — linked via BadgeDefinitionId on MeetingActivity
            var completedFunBadgeIds = await _context.ActivityCompletions
                .AsNoTracking()
                .Where(c => c.MembershipNumber == girl.MembershipNumber && c.Completed
                    && c.MeetingActivity.BadgeDefinitionId.HasValue
                    && c.MeetingActivity.BadgeDefinition!.BadgeType == BadgeType.FunBadge)
                .Select(c => c.MeetingActivity.BadgeDefinitionId!.Value)
                .Distinct()
                .ToListAsync();

            var awardedBadgeIds = await _context.AwardedBadges
                .AsNoTracking()
                .Where(a => a.MembershipNumber == girl.MembershipNumber)
                .Select(a => a.BadgeDefinitionId)
                .ToHashSetAsync();

            foreach (var funBadgeId in completedFunBadgeIds)
            {
                if (!awardedBadgeIds.Contains(funBadgeId))
                {
                    var badgeName = await _context.BadgeDefinitions
                        .AsNoTracking()
                        .Where(b => b.Id == funBadgeId)
                        .Select(b => b.Name)
                        .FirstOrDefaultAsync();

                    awards.Add(new AwardDue
                    {
                        MembershipNumber = girl.MembershipNumber,
                        Name = girl.FullName,
                        AwardName = badgeName ?? "Fun Badge",
                        AwardType = "FunBadge",
                        BadgeDefinitionId = funBadgeId
                    });
                }
            }

            // Check nights away milestones
            var totalNights = await _context.Attendances
                .Where(a => a.MembershipNumber == girl.MembershipNumber && a.Attended && a.NightsAway.HasValue && a.NightsAway > 0)
                .SumAsync(a => a.NightsAway ?? 0);

            var awardedMilestones = await _context.NightsAwayBadges
                .Where(n => n.MembershipNumber == girl.MembershipNumber)
                .Select(n => n.Milestone)
                .ToHashSetAsync();

            foreach (var milestone in NightsAwayMilestones)
            {
                if (totalNights >= milestone && !awardedMilestones.Contains(milestone))
                {
                    awards.Add(new AwardDue
                    {
                        MembershipNumber = girl.MembershipNumber,
                        Name = girl.FullName,
                        AwardName = $"Nights Away ({milestone})",
                        AwardType = "NightsAway",
                        Milestone = milestone
                    });
                }
            }
        }

        return awards;
    }

    public async Task<(bool Success, string ErrorMessage)> MarkBadgeAwardedAsync(string membershipNumber, int badgeDefinitionId)
    {
        var exists = await _context.AwardedBadges
            .AnyAsync(a => a.MembershipNumber == membershipNumber && a.BadgeDefinitionId == badgeDefinitionId);

        if (exists)
            return (true, string.Empty);

        var awarded = new AwardedBadge
        {
            MembershipNumber = membershipNumber,
            BadgeDefinitionId = badgeDefinitionId,
            DateAwarded = DateTime.Today
        };
        _context.AwardedBadges.Add(awarded);
        await _context.SaveChangesAsync();

        await _inventory.TryDecrementForBadgeAsync(badgeDefinitionId, awarded.Id);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> UnmarkBadgeAwardedAsync(string membershipNumber, int badgeDefinitionId)
    {
        var record = await _context.AwardedBadges
            .FirstOrDefaultAsync(a => a.MembershipNumber == membershipNumber && a.BadgeDefinitionId == badgeDefinitionId);

        if (record != null)
        {
            _context.AwardedBadges.Remove(record);
            await _context.SaveChangesAsync();
        }

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> SetGoldChallengeCompleteAsync(string membershipNumber, bool complete)
    {
        var tracking = await _context.AwardTrackings
            .FirstOrDefaultAsync(a => a.MembershipNumber == membershipNumber);

        if (tracking == null)
        {
            tracking = new AwardTracking
            {
                MembershipNumber = membershipNumber,
                GoldChallengeComplete = complete,
                GoldChallengeDate = complete ? DateTime.Today : null
            };
            _context.AwardTrackings.Add(tracking);
        }
        else
        {
            tracking.GoldChallengeComplete = complete;
            tracking.GoldChallengeDate = complete ? DateTime.Today : null;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> MarkThemeAwardedAsync(string membershipNumber, Theme theme)
    {
        var exists = await _context.AwardedThemeAwards
            .AnyAsync(a => a.MembershipNumber == membershipNumber && a.Theme == theme);

        if (exists)
            return (true, string.Empty);

        _context.AwardedThemeAwards.Add(new AwardedThemeAward
        {
            MembershipNumber = membershipNumber,
            Theme = theme,
            DateAwarded = DateTime.Today
        });
        await _context.SaveChangesAsync();

        await _inventory.TryDecrementForThemeAwardAsync(theme);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> MarkLevelAwardedAsync(string membershipNumber, string level)
    {
        var tracking = await _context.AwardTrackings
            .FirstOrDefaultAsync(a => a.MembershipNumber == membershipNumber);

        if (tracking == null)
        {
            tracking = new AwardTracking
            {
                MembershipNumber = membershipNumber
            };
            _context.AwardTrackings.Add(tracking);
        }

        switch (level)
        {
            case "Bronze":
                tracking.BronzeAwardedDate = DateTime.Today;
                break;
            case "Silver":
                tracking.SilverAwardedDate = DateTime.Today;
                break;
            case "Gold":
                tracking.GoldAwardedDate = DateTime.Today;
                break;
            default:
                return (false, $"Unknown level: {level}");
        }

        await _context.SaveChangesAsync();

        await _inventory.TryDecrementForLevelAsync(level);

        return (true, string.Empty);
    }

    // ===== Nights Away Badges =====

    public async Task<(bool Success, string ErrorMessage)> MarkNightsAwayBadgeAwardedAsync(string membershipNumber, int milestone)
    {
        var exists = await _context.NightsAwayBadges
            .AnyAsync(n => n.MembershipNumber == membershipNumber && n.Milestone == milestone);

        if (exists)
            return (true, string.Empty);

        _context.NightsAwayBadges.Add(new NightsAwayBadge
        {
            MembershipNumber = membershipNumber,
            Milestone = milestone,
            DateAwarded = DateTime.Today
        });
        await _context.SaveChangesAsync();

        await _inventory.TryDecrementForNightsAwayAsync(milestone);
        return (true, string.Empty);
    }

    public async Task<Dictionary<string, HashSet<int>>> GetAwardedNightsAwayMilestonesAsync(List<string> membershipNumbers)
    {
        var records = await _context.NightsAwayBadges
            .AsNoTracking()
            .Where(n => membershipNumbers.Contains(n.MembershipNumber))
            .Select(n => new { n.MembershipNumber, n.Milestone })
            .ToListAsync();

        var result = new Dictionary<string, HashSet<int>>();
        foreach (var r in records)
        {
            if (!result.ContainsKey(r.MembershipNumber))
                result[r.MembershipNumber] = new HashSet<int>();
            result[r.MembershipNumber].Add(r.Milestone);
        }
        return result;
    }

    // ===== Term Balance =====

    public async Task<TermBalance> GetTermBalanceAsync(int termId)
    {
        var term = await _context.Terms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == termId);

        if (term == null)
            return new TermBalance { TermId = termId };

        var meetings = await _context.Meetings
            .Include(m => m.MeetingActivities)
                .ThenInclude(a => a.UmaDefinition)
            .Include(m => m.MeetingActivities)
                .ThenInclude(a => a.BadgeClause)
                    .ThenInclude(c => c!.BadgeDefinition)
            .AsNoTracking()
            .Where(m => m.Date >= term.StartDate && m.Date <= term.EndDate)
            .ToListAsync();

        var balance = new TermBalance
        {
            TermId = termId,
            TermName = term.Name,
            NightsAwayOffered = meetings
                .Where(m => m.EndDate.HasValue)
                .Sum(m => (m.EndDate!.Value - m.Date).Days)
        };

        var themes = Enum.GetValues<Theme>();
        foreach (var theme in themes)
        {
            balance.ThemeBalances[theme] = new ThemeBalance { Theme = theme };
        }

        var badgesSeen = new HashSet<int>();

        foreach (var meeting in meetings)
        {
            foreach (var activity in meeting.MeetingActivities)
            {
                if (activity.UmaDefinition != null)
                {
                    balance.ThemeBalances[activity.UmaDefinition.Theme].MinutesPlanned += activity.UmaDefinition.Minutes;
                    balance.ThemeBalances[activity.UmaDefinition.Theme].UmaMinutesPlanned += activity.UmaDefinition.Minutes;
                    balance.TotalMinutesPlanned += activity.UmaDefinition.Minutes;
                    balance.TotalUmaMinutesPlanned += activity.UmaDefinition.Minutes;
                }

                if (activity.BadgeClause?.BadgeDefinition != null)
                {
                    var clause = activity.BadgeClause;
                    var badge = clause.BadgeDefinition;

                    var firstTimeSeen = badgesSeen.Add(badge.Id);

                    if (badge.Theme.HasValue)
                    {
                        if (firstTimeSeen)
                        {
                            var tb = balance.ThemeBalances[badge.Theme.Value];
                            tb.BadgesWorkedOn++;
                            balance.TotalBadgesWorkedOn++;

                            if (badge.BadgeType == BadgeType.SkillsBuilder)
                                tb.SkillsBuildersWorkedOn++;
                            else if (badge.BadgeType == BadgeType.InterestBadge)
                                tb.InterestBadgesWorkedOn++;
                        }

                        // Add clause's estimated minutes to the theme total
                        if (clause.EstimatedMinutes > 0)
                        {
                            balance.ThemeBalances[badge.Theme.Value].MinutesPlanned += clause.EstimatedMinutes;
                            balance.TotalMinutesPlanned += clause.EstimatedMinutes;
                        }
                    }
                    else if (firstTimeSeen && badge.BadgeType == BadgeType.FunBadge)
                    {
                        balance.FunBadgesWorkedOn++;
                    }
                }
            }
        }

        // Calculate percentages
        if (balance.TotalMinutesPlanned > 0)
        {
            foreach (var tb in balance.ThemeBalances.Values)
            {
                tb.PercentageOfTotal = (double)tb.MinutesPlanned / balance.TotalMinutesPlanned * 100;
            }
        }

        return balance;
    }

    // ===== Standalone Completions =====

    public async Task<(bool Success, string ErrorMessage)> SaveStandaloneCompletionAsync(
        string membershipNumber, int? badgeClauseId, int? umaDefinitionId, bool completed)
    {
        if (!badgeClauseId.HasValue && !umaDefinitionId.HasValue)
            return (false, "Must specify a badge clause or UMA definition.");

        // Find existing standalone MeetingActivity for this clause/UMA
        var activityQuery = _context.MeetingActivities
            .Where(a => a.MeetingId == null);

        if (badgeClauseId.HasValue)
            activityQuery = activityQuery.Where(a => a.BadgeClauseId == badgeClauseId);
        else
            activityQuery = activityQuery.Where(a => a.UmaDefinitionId == umaDefinitionId);

        var activity = await activityQuery.FirstOrDefaultAsync();

        if (completed)
        {
            // Create MeetingActivity if needed
            if (activity == null)
            {
                string name;
                if (badgeClauseId.HasValue)
                {
                    var clause = await _context.BadgeClauses.FindAsync(badgeClauseId.Value);
                    name = clause?.Name ?? "Standalone completion";
                }
                else
                {
                    var uma = await _context.Set<UmaDefinition>().FindAsync(umaDefinitionId!.Value);
                    name = uma?.Name ?? "Standalone UMA";
                }

                activity = new MeetingActivity
                {
                    MeetingId = null,
                    Name = name,
                    BadgeClauseId = badgeClauseId,
                    UmaDefinitionId = umaDefinitionId
                };
                _context.MeetingActivities.Add(activity);
                await _context.SaveChangesAsync();
            }

            // Upsert completion
            var existing = await _context.ActivityCompletions
                .FirstOrDefaultAsync(c => c.MeetingActivityId == activity.Id
                    && c.MembershipNumber == membershipNumber);

            if (existing == null)
            {
                _context.ActivityCompletions.Add(new ActivityCompletion
                {
                    MeetingActivityId = activity.Id,
                    MembershipNumber = membershipNumber,
                    Completed = true
                });
            }
            else
            {
                existing.Completed = true;
            }
        }
        else
        {
            // Unchecking: remove completion, clean up orphaned activity
            if (activity != null)
            {
                var completion = await _context.ActivityCompletions
                    .FirstOrDefaultAsync(c => c.MeetingActivityId == activity.Id
                        && c.MembershipNumber == membershipNumber);

                if (completion != null)
                    _context.ActivityCompletions.Remove(completion);

                // If no other completions remain, remove the standalone activity
                var otherCompletions = await _context.ActivityCompletions
                    .CountAsync(c => c.MeetingActivityId == activity.Id
                        && c.MembershipNumber != membershipNumber);

                if (otherCompletions == 0)
                    _context.MeetingActivities.Remove(activity);
            }
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<List<StandaloneCompletionDto>> GetStandaloneCompletionsAsync(string membershipNumber)
    {
        return await _context.ActivityCompletions
            .AsNoTracking()
            .Where(c => c.MembershipNumber == membershipNumber && c.Completed
                && c.MeetingActivity.MeetingId == null)
            .Select(c => new StandaloneCompletionDto
            {
                BadgeClauseId = c.MeetingActivity.BadgeClauseId,
                UmaDefinitionId = c.MeetingActivity.UmaDefinitionId
            })
            .ToListAsync();
    }

    public async Task<HashSet<int>> GetAllCompletedUmaIdsAsync(string membershipNumber)
    {
        // Get all completed activity IDs for this girl
        var completedActivityIds = await _context.ActivityCompletions
            .AsNoTracking()
            .Where(c => c.MembershipNumber == membershipNumber && c.Completed)
            .Select(c => c.MeetingActivityId)
            .ToListAsync();

        // Get UMA definition IDs from all completed activities (meeting-based and standalone)
        var umaIds = await _context.MeetingActivities
            .AsNoTracking()
            .Where(a => completedActivityIds.Contains(a.Id) && a.UmaDefinitionId.HasValue)
            .Select(a => a.UmaDefinitionId!.Value)
            .Distinct()
            .ToListAsync();

        return umaIds.ToHashSet();
    }

    public async Task<HashSet<int>> GetStandaloneCompletedUmaIdsAsync(string membershipNumber)
    {
        // Get UMA IDs completed via standalone (no meeting) - these can be toggled
        var umaIds = await _context.ActivityCompletions
            .AsNoTracking()
            .Where(c => c.MembershipNumber == membershipNumber && c.Completed
                && c.MeetingActivity.MeetingId == null
                && c.MeetingActivity.UmaDefinitionId.HasValue)
            .Select(c => c.MeetingActivity.UmaDefinitionId!.Value)
            .Distinct()
            .ToListAsync();

        return umaIds.ToHashSet();
    }

    // ===== Helpers =====

    private AwardStatus CalculateAwardStatus(List<GirlThemeProgress> themeProgress, string membershipNumber)
    {
        return new AwardStatus
        {
            ThemeAwardsEarned = themeProgress.Count(t => t.ThemeAwardEarned)
        };
    }

    private static int GetUmaMinutesRequired(Section? section) => section switch
    {
        Section.Rainbow => 120,
        Section.Brownie => 180,
        Section.Guide => 240,
        Section.Ranger => 240,
        _ => 180
    };

    public async Task<List<AwardDue>> GetProjectedAwardsForCurrentTermAsync()
    {
        var today = DateTime.Today;

        // Get the current term
        var currentTerm = await _context.Terms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.StartDate <= today && t.EndDate >= today);

        if (currentTerm == null)
            return new List<AwardDue>();

        // Badge clause IDs that appear in future meetings still to come this term
        var futurePlannedClauseIds = await _context.MeetingActivities
            .AsNoTracking()
            .Where(a => a.MeetingId.HasValue
                     && a.BadgeClauseId.HasValue
                     && a.Meeting!.Date > today
                     && a.Meeting!.Date <= currentTerm.EndDate)
            .Select(a => a.BadgeClauseId!.Value)
            .Distinct()
            .ToHashSetAsync();

        if (!futurePlannedClauseIds.Any())
            return new List<AwardDue>();

        // Only badges that have at least one clause appearing in future meetings
        var allBadges = await _context.BadgeDefinitions
            .Include(b => b.Clauses)
            .AsNoTracking()
            .ToListAsync();

        var relevantBadges = allBadges
            .Where(b => b.Clauses.Any(c => futurePlannedClauseIds.Contains(c.Id)))
            .ToList();

        if (!relevantBadges.Any())
            return new List<AwardDue>();

        // Active girls
        var girls = await _context.Persons
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDataRemoved && p.PersonType == PersonType.Girl)
            .ToListAsync();

        // Awarded badge IDs per girl (to skip already-awarded badges)
        var awardedRows = await _context.AwardedBadges
            .AsNoTracking()
            .Select(a => new { a.MembershipNumber, a.BadgeDefinitionId })
            .ToListAsync();

        var awardedByGirl = awardedRows
            .GroupBy(a => a.MembershipNumber)
            .ToDictionary(g => g.Key, g => g.Select(a => a.BadgeDefinitionId).ToHashSet());

        // Current clause completions per girl (all activities, including standalone)
        var completionRows = await _context.ActivityCompletions
            .AsNoTracking()
            .Where(c => c.Completed && c.MeetingActivity.BadgeClauseId.HasValue)
            .Select(c => new { c.MembershipNumber, ClauseId = c.MeetingActivity.BadgeClauseId!.Value })
            .ToListAsync();

        var completionsByGirl = completionRows
            .GroupBy(x => x.MembershipNumber)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ClauseId).ToHashSet());

        var projected = new List<AwardDue>();

        foreach (var girl in girls)
        {
            var completedClauses = completionsByGirl.GetValueOrDefault(girl.MembershipNumber, new HashSet<int>());
            var awarded = awardedByGirl.GetValueOrDefault(girl.MembershipNumber, new HashSet<int>());

            foreach (var badge in relevantBadges)
            {
                if (awarded.Contains(badge.Id))
                    continue;

                var clauseIds = badge.Clauses.Select(c => c.Id).ToHashSet();
                var currentCount = clauseIds.Count(id => completedClauses.Contains(id));

                // Already complete — skip (these appear in GetAwardsDueAsync instead)
                if (currentCount >= badge.RequiredCompletions)
                    continue;

                // Would complete if the girl attended all remaining meetings
                var projectedCount = clauseIds.Count(id => completedClauses.Contains(id) || futurePlannedClauseIds.Contains(id));

                if (projectedCount >= badge.RequiredCompletions)
                {
                    projected.Add(new AwardDue
                    {
                        MembershipNumber = girl.MembershipNumber,
                        Name = girl.FullName,
                        AwardName = badge.Name,
                        AwardType = "Badge",
                        BadgeDefinitionId = badge.Id
                    });
                }
            }
        }

        return projected;
    }

    public static string GetThemeDisplayName(Theme theme) => theme switch
    {
        Theme.KnowMyself => "Know Myself",
        Theme.ExpressMyself => "Express Myself",
        Theme.BeWell => "Be Well",
        Theme.HaveAdventures => "Have Adventures",
        Theme.TakeAction => "Take Action",
        Theme.SkillsForMyFuture => "Skills For My Future",
        _ => theme.ToString()
    };
}

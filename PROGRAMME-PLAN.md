# Programme Management System Plan

## Overview

Extend GUMS from attendance/finance into a Girlguiding programme management system. The core idea: define reusable badge/activity definitions, link them to meetings, track per-girl completions, and calculate Theme Award / Bronze / Silver / Gold progress automatically.

## Data Model

### New Enums

**Theme** - The 6 programme themes:
`Know Myself`, `Express Myself`, `Be Well`, `Have Adventures`, `Take Action`, `Skills For My Future`

**BadgeType** - `SkillsBuilder`, `InterestBadge`

### New Entities

#### `BadgeDefinition`
A reusable badge record (e.g. "First Aid Stage 3", "Baking Interest Badge - Brownies").
- `Id`, `Name`, `Theme` (enum), `BadgeType` (enum)
- `Section` (enum) - which section this badge belongs to
- `Stage` (int?, nullable) - for skills builders only (1-6)
- `SkillsBuilderName` (string?, nullable) - e.g. "Lead", "Life Skills" - groups the 6 stages
- `RequiredCompletions` (int, default 4 for skills builders, 3 for interest badges) - how many clauses needed
- Navigation: `Clauses` collection

#### `BadgeClause`
One clause/requirement of a badge (e.g. "Clause 1: Learn about first aid kits").
- `Id`, `BadgeDefinitionId` (FK), `Name`, `Description`, `SortOrder`

#### `UmaDefinition`
A reusable Unit Meeting Activity definition.
- `Id`, `Name`, `Description`, `Theme` (enum), `Minutes` (int)

#### `MeetingActivity`
Links a meeting to either a badge clause or a UMA. Replaces/extends the current `Activity` entity.
- `Id`, `MeetingId` (FK), `Name`, `Description`
- `BadgeClauseId` (int?, FK, nullable) - if this activity is working on a badge clause
- `UmaDefinitionId` (int?, FK, nullable) - if this is a UMA
- `RequiresConsent`, `SortOrder`
- Navigation: `Completions` collection

#### `ActivityCompletion`
Per-girl record of completing a meeting activity.
- `Id`, `MeetingActivityId` (FK), `MembershipNumber` (string, not FK - GDPR pattern)
- `Completed` (bool)

### Changes to Existing Entities

- **Activity** - Replaced by `MeetingActivity` (migration renames table, adds new columns)
- **Meeting** - Navigation property changes from `Activities` to `MeetingActivities`

### Calculated / Derived (not stored)

- **Badge completion**: A girl has completed a badge when she has `RequiredCompletions` distinct clauses marked complete via `ActivityCompletion`
- **UMA hours per theme**: Sum of `UmaDefinition.Minutes` for all UMAs the girl completed, grouped by theme
- **Theme Award**: Earned when girl has 1 completed skills builder + 1 completed interest badge + required UMA hours (Rainbow: 120min, Brownie: 180min, Guide/Ranger: 240min) for that theme
- **Bronze**: 2 Theme Awards, **Silver**: 4 Theme Awards, **Gold**: 6 Theme Awards + additional challenge (tracked as a boolean flag per girl)

## New Services

### `IBadgeService` / `BadgeService`
- CRUD for `BadgeDefinition`, `BadgeClause`, `UmaDefinition`
- Search/filter badges by theme, section, type
- Get badges previously used (for reuse when planning meetings)

### `IProgrammeService` / `ProgrammeService`
- Record activity completions (bulk: all attendees, or per-girl)
- Get girl's progress: which badges complete, which clauses done, UMA hours per theme
- Get theme award status per girl
- Get award status (bronze/silver/gold) per girl
- Get unit-wide progress matrix (all girls x all themes)
- Get term programme balance: minutes per theme planned this term
- Get "badges to award" list: recently completed badges not yet physically awarded
- Get "programme gaps": themes with least coverage across the unit

### Changes to `IMeetingService`
- When adding activities to a meeting, support linking to badge clauses or UMA definitions
- Support browsing/searching existing badge clauses to add to a meeting

## New UI Pages

### Programme Section (new nav group)

1. **Programme > Badges** (`/programme/badges`) - Browse/add/edit badge definitions. Filter by theme, section, type. Add clauses to badges.

2. **Programme > UMAs** (`/programme/umas`) - Browse/add/edit UMA definitions. Filter by theme.

3. **Programme > Girl Progress** (`/programme/progress/{membershipNumber}`) - Per-girl view: 6 theme cards showing skills builder %, interest badge %, UMA hours vs target. Theme Award status. Bronze/Silver/Gold progress.

4. **Programme > Unit Overview** (`/programme/overview`) - Grid: rows = active girls, columns = 6 themes. Cells show progress indicator (e.g. colour-coded: not started / in progress / theme award earned). Summary row showing coverage gaps.

5. **Programme > Awards Due** (`/programme/awards`) - List of girls who have newly completed badges/awards that need to be physically handed out. Mark as "awarded" to clear from list.

6. **Programme > Term Balance** (`/programme/term-balance`) - For selected term: pie/bar chart of planned UMA minutes by theme. Count of badges being worked on by theme. Helps plan a balanced programme.

### Changes to Existing Pages

- **Meeting Edit / Add**: Activity section gains a "Link to Badge Clause" and "Link to UMA" option when adding activities. Searchable dropdown to find existing badge clauses or UMAs. Can still add unlinked activities.

- **Record Attendance**: After marking attendance, option to "Record Completions" - defaults all attendees to completed for all activities, with ability to untick individual girls for individual activities.

## Implementation Order

### Phase 1: Data Model & Migration
- New enums, entities, DbContext configuration
- EF migration
- Rename Activity to MeetingActivity with new optional columns

### Phase 2: Badge & UMA Management
- `BadgeService` for CRUD operations
- Badge management page (add/edit badges, clauses)
- UMA management page (add/edit UMAs)

### Phase 3: Meeting Integration
- Update `MeetingService` to support linking activities to badge clauses/UMAs
- Update meeting edit pages with badge/UMA linking UI
- Activity completion recording on attendance page

### Phase 4: Programme Tracking
- `ProgrammeService` for progress calculations
- Per-girl progress page
- Unit overview page

### Phase 5: Planning & Awards
- Awards due page
- Term balance page
- Programme gap analysis

### Phase 6: Gold Award
- Add `GoldChallengeComplete` boolean to a new `AwardTracking` entity or similar
- UI for marking gold challenge complete

## Testing
- Unit tests for `BadgeService` and `ProgrammeService`
- Test badge completion logic (4 of 5 clauses = complete)
- Test theme award calculation (skills builder + interest badge + UMA hours)
- Test bronze/silver/gold derivation
- Test per-section UMA hour thresholds
- Run existing tests to ensure no regressions

## Files to Create/Modify

### New Files
- `Data/Enums/Theme.cs`, `Data/Enums/BadgeType.cs`
- `Data/Entities/BadgeDefinition.cs`, `BadgeClause.cs`, `UmaDefinition.cs`, `MeetingActivity.cs`, `ActivityCompletion.cs`
- `Services/IBadgeService.cs`, `Services/BadgeService.cs`
- `Services/IProgrammeService.cs`, `Services/ProgrammeService.cs`
- `Components/Pages/Programme/` - 6 new pages
- `Tests/BadgeServiceTests.cs`, `Tests/ProgrammeServiceTests.cs`

### Modified Files
- `Data/GUMSContext.cs` - Add new DbSets, configure relationships
- `Data/Entities/Meeting.cs` - Update navigation property
- `Services/IMeetingService.cs` / `MeetingService.cs` - Badge/UMA linking
- `Services/IAttendanceService.cs` / `AttendanceService.cs` - Activity completion recording
- `Components/Layout/NavMenu.razor` - Add Programme nav section
- `Components/Pages/Meetings/EditMeeting.razor` - Badge/UMA linking UI
- `Components/Pages/Meetings/RecordAttendance.razor` - Completion recording
- `Program.cs` - Register new services
- Existing Activity references throughout the codebase

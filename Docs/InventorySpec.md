# GUMS Inventory Management — Feature Specification

**Status:** Draft
**Date:** 2026-02-25
**Context:** GUMS is a Blazor Server app (.NET 10, SQLite, EF Core) managing a Girlguiding unit.

---

## 1. Goals

1. **Know what's in stock** — maintain a running count of physical badges and awards.
2. **Forecast demand** — predict how many of each badge will be needed to the end of the current term, based on who is on track to earn them, so a leader can order before running out.
3. **Record purchases and generate claims** — when a leader buys badges, record the cost and optionally create a reimbursement expense claim in the existing accounting system.
4. **Handle the unexpected** — visitors, lost badges, and bulk donations must all be manually adjustable with a clear audit trail.

---

## 2. Scope

### In scope

- Stock items for: Skill Builder badges, Interest Badges, Fun Badges, Theme Award badges (Bronze, Silver, Gold), Nights Away badges
- Stock transactions: purchase (with optional expense claim), award (linked to existing badge award records), manual adjustment, visitor award, loss/damage
- Term demand forecast: known awards + optimistic projection for in-progress badges
- Low-stock alerts on the dashboard
- Per-badge unit cost (used for expense claim generation)

### Out of scope (for now)

- Multi-location stock (one unit, one stock location)
- Supplier management / purchase orders
- Barcode scanning
- Grandfathered badge types that no longer exist in the database (can be added as "Other" stock items)
- Automatic procurement workflows

---

## 3. Data Model

### 3.1 New entity: `BadgeStockItem`

One row per physical badge type the unit holds stock of.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | int PK | |
| `Name` | string(200) | Display name, e.g. "Guide Skills Builder – My Wellbeing" |
| `StockType` | `BadgeStockType` enum | See §3.4 |
| `BadgeDefinitionId` | int? FK | Populated for SkillsBuilder / InterestBadge / FunBadge |
| `ThemeAwardLevel` | `ThemeAwardLevel` enum? | Bronze, Silver, or Gold — for theme award physical badges |
| `NightsAwayTier` | int? | 2, 5, 10, 20 (nights) — for nights away physical awards |
| `UnitCost` | decimal? | Cost per badge in £. Null = free / not tracked |
| `ReorderThreshold` | int? | Show low-stock warning when quantity falls below this |
| `CurrentQuantity` | int | Maintained automatically by summing transactions (no manual edit — use adjustments) |
| `Notes` | string? | Free text, e.g. "ordered in bulk from county" |
| `IsActive` | bool | Soft-delete for discontinued items |

**Unique constraint:** at most one active stock item per `BadgeDefinitionId`; at most one per `(StockType=ThemeAward, ThemeAwardLevel)`; at most one per `(StockType=NightsAway, NightsAwayTier)`.

---

### 3.2 New entity: `BadgeStockTransaction`

Immutable ledger. `CurrentQuantity` on `BadgeStockItem` equals the sum of all transaction `Quantity` values for that item. Never edit or delete rows — use a reversing adjustment instead.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | int PK | |
| `BadgeStockItemId` | int FK | |
| `TransactionDate` | DateTime | |
| `Quantity` | int | Positive = stock in, negative = stock out |
| `TransactionType` | `StockTransactionType` enum | See §3.5 |
| `UnitCost` | decimal? | Cost per badge at time of transaction (for purchases) |
| `TotalCost` | decimal? | `UnitCost × |Quantity|` — stored for audit, not recalculated |
| `AwardedBadgeId` | int? FK | Set when type = Award, links to `AwardedBadge` |
| `AwardedThemeAwardId` | int? FK | Set when type = Award, links to `AwardedThemeAward` |
| `ExpenseClaimId` | int? FK | Set when a purchase creates an expense claim |
| `Notes` | string(500)? | Mandatory for ManualAdjustment and Loss; optional otherwise |
| `CreatedDate` | DateTime | UTC |

---

### 3.3 Modifications to existing entities

**`BadgeDefinition`** — no changes needed. The `BadgeStockItem.BadgeDefinitionId` FK points here.

**`AwardedBadge`** and **`AwardedThemeAward`** — no changes to schema. When a badge is awarded in the UI, the system optionally creates a `BadgeStockTransaction` of type `Award` and decrements the relevant `BadgeStockItem`.

---

### 3.4 New enum: `BadgeStockType`

```csharp
public enum BadgeStockType
{
    SkillsBuilder,    // links via BadgeDefinitionId
    InterestBadge,    // links via BadgeDefinitionId
    FunBadge,         // links via BadgeDefinitionId
    ThemeAward,       // links via ThemeAwardLevel
    NightsAway,       // links via NightsAwayTier
    Other             // free-standing item with no FK link
}
```

---

### 3.5 New enum: `StockTransactionType`

```csharp
public enum StockTransactionType
{
    Purchase,           // leader bought stock; qty positive; may link to ExpenseClaim
    Award,              // given to a member; qty negative; links to AwardedBadge / AwardedThemeAward
    VisitorAward,       // given to a visitor (no member record); qty negative
    ManualIn,           // found extras, returned, donated; qty positive; notes required
    ManualOut,          // lost, damaged, given away; qty negative; notes required
    Adjustment,         // stocktake correction (can be positive or negative); notes required
}
```

---

### 3.6 New enum: `ThemeAwardLevel`

```csharp
public enum ThemeAwardLevel
{
    Bronze,
    Silver,
    Gold
}
```

---

## 4. Service Layer

### `IInventoryService` (new)

```csharp
// Stock item management
Task<List<BadgeStockItem>> GetAllStockItemsAsync();
Task<BadgeStockItem?> GetStockItemByIdAsync(int id);
Task<BadgeStockItem?> GetStockItemForBadgeAsync(int badgeDefinitionId);
Task<BadgeStockItem?> GetStockItemForThemeAwardAsync(ThemeAwardLevel level);
Task<BadgeStockItem?> GetStockItemForNightsAwayAsync(int tier);
Task<(bool Success, string ErrorMessage, BadgeStockItem? Item)> CreateStockItemAsync(BadgeStockItem item);
Task<(bool Success, string ErrorMessage)> UpdateStockItemAsync(BadgeStockItem item);

// Transactions
Task<List<BadgeStockTransaction>> GetTransactionsForItemAsync(int stockItemId);
Task<(bool Success, string ErrorMessage, BadgeStockTransaction? Txn)> RecordTransactionAsync(BadgeStockTransaction txn);

// Called from badge award flows
Task RecordAwardTransactionAsync(int badgeDefinitionId, string membershipNumber, int awardedBadgeId);
Task RecordThemeAwardTransactionAsync(ThemeAwardLevel level, string membershipNumber, int awardedThemeAwardId);

// Forecast
Task<List<InventoryForecastLine>> GetTermForecastAsync(int termId);

// Alerts
Task<List<LowStockAlert>> GetLowStockAlertsAsync();
```

### `InventoryForecastLine` (DTO)

```csharp
public class InventoryForecastLine
{
    public BadgeStockItem StockItem { get; set; }
    public int CurrentStock { get; set; }
    public int AlreadyAwardedThisTerm { get; set; }    // confirmed
    public int ProjectedThisTerm { get; set; }          // on-track (optimistic)
    public int TotalDemand => AlreadyAwardedThisTerm + ProjectedThisTerm;
    public int NetPosition => CurrentStock - TotalDemand; // negative = shortfall
    public List<string> AwardedMemberNames { get; set; }    // for drilling down
    public List<string> ProjectedMemberNames { get; set; }
}
```

---

## 5. Term Demand Forecast Logic

The forecast answers: *"If we run all planned meetings this term as written, and everyone attends who can, how many of each badge will we need?"*

### 5.1 Already awarded this term

- Query `AwardedBadge` where `DateAwarded >= term.StartDate && DateAwarded <= term.EndDate`
- Query `AwardedThemeAward` similarly
- These are **certain** — the badge has already been given out (or needs to be given out)

### 5.2 Projected awards (on-track members)

For **skills builder / interest badges:**

1. For each girl who has **not yet** earned a given badge this term:
   a. Find how many clauses she has completed (`ActivityCompletion` records + manually ticked clauses)
   b. Find how many remaining clauses are linked (via `MeetingActivity.BadgeClauseId`) to future meetings this term
   c. If `completed + future-linked >= badge.RequiredCompletions` → she is **on track**
2. If she is on track, add her to the `ProjectedMemberNames` list for that badge

For **fun badges:**
- A girl is projected if she has a future meeting activity linked to `BadgeDefinitionId` for this badge and she hasn't already earned it

For **theme award badges:**
- Bronze: girl has earned all clauses for 2 theme badges (via `AwardedBadge` or on-track as above), has not yet earned Bronze for any theme — but this gets complex. **Simplification for v1:** count girls who are within 1 badge of their next theme award level based on current `AwardedBadge` and `AwardedThemeAward` counts.
- Same approach for Silver (6 theme badges) and Gold.

For **nights away badges:**
- Query cumulative `NightsAway` from `Attendance` for each girl
- Include future multi-day meetings in the term (everyone attending, or only consented for events requiring consent)
- If crossing a nights-away tier threshold, flag as projected

### 5.3 Consent gating

For any **future meeting that has `RequiresConsent = true`**:
- In "optimistic" mode: assume all girls who have consented **or not yet responded** will attend
- In "conservative" mode: only count girls who have `ConsentFormReceived = true`
- The UI should show both scenarios (or let the leader toggle)

### 5.4 Exclusions

- Girls who are **inactive** or have **data removed** are excluded
- If a badge is not linked to a stock item, that badge type is omitted from the forecast (the item hasn't been set up yet)

---

## 6. Purchase Flow and Expense Claims

When a leader buys badges:

1. Navigate to the stock item
2. Click **Record Purchase**
3. Enter: quantity, date, unit cost (pre-filled from stock item's `UnitCost`), optional notes
4. Optionally tick **Create expense claim** — this creates an `Expense` record in the accounting module:
   - `Amount` = quantity × unit cost
   - `Description` = auto-generated, e.g. "6 × Guide Fun Badge – Cookie Monster (@ £1.20)"
   - `ExpenseAccountId` = a designated "Badge Purchases" expense account (configurable, or let leader choose)
   - The `Expense.ExpenseClaimId` links it into an existing or new `ExpenseClaim`
5. On save, a `BadgeStockTransaction` of type `Purchase` is created (positive quantity), and `BadgeStockItem.CurrentQuantity` is updated

---

## 7. Award Integration

### Automatic (preferred)

When a badge is awarded on the Girl Progress page (or any future "award badge" flow):

- The service layer checks if a `BadgeStockItem` exists for this badge type
- If yes, a `BadgeStockTransaction` of type `Award` (qty = −1) is created automatically, linked to the `AwardedBadgeId` / `AwardedThemeAwardId`
- `CurrentQuantity` is decremented
- If `CurrentQuantity` would go negative, show a **warning** (not a block) — stock may be correct after a stocktake, and the leader should know

### Manual (for missed awards)

Leaders can record past award transactions manually from the stock item detail page: enter date, link to a girl, optionally link to an `AwardedBadge` record.

### Visitor awards

On the stock adjustment page, a `VisitorAward` transaction (qty = −1) with notes (e.g. "visitor from Brownies on 14 Feb").

---

## 8. UI Pages

### 8.1 `/Inventory` — Stock Overview

- **Nav:** Accounts → Inventory (or its own top-level nav entry)
- **Cards:** one per stock type group (Skills Builders, Interest Badges, Fun Badges, Theme Awards, Nights Away)
- **Table per group:** Name | In Stock | Pending Demand (this term) | Net | Unit Cost | Actions
- **Traffic-light colour:**
  - Green: Net ≥ 0 and above reorder threshold
  - Amber: Below reorder threshold but not negative
  - Red: Net < 0 (shortfall)
- **Low stock alert banner** at top if any items are amber or red
- **Button:** Add Stock Item

### 8.2 `/Inventory/Stock/{id}` — Stock Item Detail

- Header: name, current quantity, unit cost, reorder threshold
- **Transaction history table** (newest first): Date | Type | Qty | Running total | Notes | Linked award/claim
- **Actions:** Record Purchase | Record Adjustment | Record Visitor Award
- Edit button → goes to Edit page

### 8.3 `/Inventory/Stock/Edit/{id}` (and `/New`) — Edit Stock Item

Fields: Name, Stock Type, link to Badge Definition (select from list), Theme Award Level (if ThemeAward type), Nights Away Tier (if NightsAway type), Unit Cost, Reorder Threshold, Notes, Is Active.

### 8.4 `/Inventory/Adjust/{id}` — Record Transaction

Used for: Purchase, ManualIn, ManualOut, VisitorAward, Adjustment.

Fields:
- Transaction Type (select)
- Date (default today)
- Quantity (positive or negative depending on type — UI enforces sign)
- Unit Cost (shown/required only for Purchase)
- Notes (required for ManualOut, VisitorAward, Adjustment; optional for Purchase, ManualIn)
- **Create expense claim** checkbox (shown only for Purchase, when stock item has a unit cost)

### 8.5 `/Inventory/Forecast` — Term Demand Forecast

- Term selector (default: current term)
- Toggle: **Optimistic** (all girls who haven't said no) vs **Conservative** (confirmed consent only)
- Table: Badge | In Stock | Already Awarded | Projected | Total Demand | Net | Action
- Expandable rows: click to see list of projected earners by name
- **Order summary button** → generates a plain-text/printable shopping list of items where Net < 0, with quantities and costs

---

## 9. Dashboard Integration

Add to the existing **Accounts** dashboard card (or a new **Inventory** widget):

- Count of stock items with Net < 0 (shortfall this term)
- Count of stock items below reorder threshold
- Link to `/Inventory`

---

## 10. New Nav Menu Entry

Under **Accounts** (or alongside it):

```
Inventory
  ├── Stock Overview       /Inventory
  └── Term Forecast        /Inventory/Forecast
```

---

## 11. EF Migrations Required

1. `AddInventoryTables` — creates `BadgeStockItems` and `BadgeStockTransactions` tables, adds new enums
2. No changes to existing `BadgeDefinition`, `AwardedBadge`, or `AwardedThemeAward` tables

---

## 12. Implementation Order (suggested)

| Phase | What | Value |
|-------|------|-------|
| 1 | Entities, migrations, `IInventoryService` stub | Foundation |
| 2 | `/Inventory` stock overview + Add/Edit stock items | Leaders can start recording stock |
| 3 | `/Inventory/Stock/{id}` detail + Manual transactions | Full stock management |
| 4 | Award integration (auto-decrement on badge award) | Stock stays accurate without manual work |
| 5 | Purchase flow + expense claim generation | Reimbursement workflow |
| 6 | `/Inventory/Forecast` — term demand forecast | Planning ahead |
| 7 | Dashboard widget + low stock alerts | Visibility |

---

## 13. Open Questions

1. **Theme award physical badge granularity.** Are Bronze, Silver, and Gold distinct physical items (different woven badges)? Or is it always the same "theme award badge" regardless of level? The spec assumes three distinct items (one stock row per level) but this can be collapsed to one if they look identical.

2. **Who triggers the award decrement?** The Girl Progress page is where a leader manually records badge completions. Should clicking "Award Badge" there immediately decrement stock? Or should there be a separate "distribute badges" step? The spec assumes immediate decrement with a warning if stock goes negative.

3. **Badge Purchases expense account.** The existing chart of accounts may not have a "Badge Purchases" category. This either needs to be pre-seeded or configured in Unit Configuration. Recommendation: add a `BadgePurchasesAccountId` field to `UnitConfiguration`.

4. **NightsAway badge tiers.** The existing system has a `NightsAwayBadge` entity (from the `AddNightsAwayBadgesAndExtraNights` migration). The inventory items for nights away should align with whatever tiers are defined there rather than hard-coding 2/5/10/20.

5. **Conservative vs optimistic for regular meetings.** Regular meetings don't require consent, so everyone is assumed to attend. Is this always appropriate, or should absence rate history factor in? For v1, assume 100% attendance for projection purposes (conservative enough for stock planning).

---

## 14. Related Files (for implementation reference)

| File | Relevance |
|------|-----------|
| `GUMS/Data/Entities/BadgeDefinition.cs` | Badge types to link stock items to |
| `GUMS/Data/Entities/AwardedBadge.cs` | Source of truth for confirmed awards |
| `GUMS/Data/Entities/AwardedThemeAward.cs` | Theme award confirmation |
| `GUMS/Data/Entities/Expense.cs` | Target for expense claim creation |
| `GUMS/Data/Entities/ExpenseClaim.cs` | Claim to attach purchase expenses to |
| `GUMS/Services/IProgrammeService.cs` | Badge progress queries (re-use for forecast) |
| `GUMS/Services/IAttendanceService.cs` | Attendance + consent data for forecast |
| `GUMS/Services/IMeetingService.cs` | Future meeting + activity data for forecast |
| `GUMS/Components/Pages/Programme/GirlProgress.razor` | Where award decrement hook should fire |
| `GUMS/Components/Pages/Meetings/RecordAttendance.razor` | Activity completion — also a potential hook |

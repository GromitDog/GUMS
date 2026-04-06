# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build GUMS/GUMS.csproj

# Run
dotnet run --project GUMS/GUMS.csproj

# Run tests
dotnet test GUMS.Tests/GUMS.Tests.csproj

# Run a single test class
dotnet test GUMS.Tests/GUMS.Tests.csproj --filter "FullyQualifiedName~PaymentServiceTests"

# Run a single test method
dotnet test GUMS.Tests/GUMS.Tests.csproj --filter "FullyQualifiedName~PaymentServiceTests.RecordPayment_SetsStatusToPaid"

# EF Core migrations
dotnet ef migrations add <Name> --project GUMS
dotnet ef database update --project GUMS
```

**Build note:** MSB3026/MSB3027 file-lock errors occur when Rider has the app running. These are NOT compile errors — only `error CS` lines indicate real failures.

## Architecture

**Stack:** ASP.NET Core Blazor Server (.NET 10.0), EF Core 9.0 with SQLite, ASP.NET Core Identity

**Database:** SQLite at `%APPDATA%\GUMS\gums.db`. Auto-migrated on startup. Windows file permissions enforced.

### Service Layer

All business logic lives in `GUMS/Services/`. Each service has an interface (`IXxxService.cs`) and implementation (`XxxService.cs`), registered as `AddScoped` in `Program.cs`.

**Conventions:**
- Methods returning success/failure use `(bool Success, string ErrorMessage)` tuples
- Read queries use `AsNoTracking()`
- All I/O is async
- The `IAccountingService` is injected as nullable (`IAccountingService?`) in some services to allow operation without accounting setup

### Double-Entry Accounting

The accounting system uses proper double-entry bookkeeping:
- `Account` entities with types: Asset, Liability, Equity, Income, Expense
- `Transaction` (journal entry) contains balanced `TransactionLine` entries (debit/credit)
- System account codes: `1001` Cash, `1002` Cheques, `1003` Bank, `2001` Member Credits (liability), `4001` Subs Income, `4002` Activity Income, `3001` Opening Balances
- `CreateTransactionAsync` auto-updates account balances and validates debit=credit
- Account constants defined at top of `AccountingService.cs`

### Payment System

Payments link to members via `MembershipNumber` (string), NOT `PersonId` FK. This allows payment records to persist after GDPR data removal.

**Payment flow:** Pending → Paid (when `AmountPaid >= Amount`) → optionally Refunded or converted to Member Credit.

**Member Credit system:** Paid payments can be converted to credit (stored in `MemberCredit`/`CreditTransaction` tables). Credit can be applied to pending payments or refunded as cash. Accounting entries move amounts between income and the Member Credits liability account.

### Pages & Components

Pages are in `GUMS/Components/Pages/`, organized by feature area (Register, Meetings, Payments, Accounts, Programme, Inventory, Configuration, Reports). Each page typically has a `.razor` and `.razor.cs` code-behind.

**Render mode:** Interactive Server (`@rendermode InteractiveServer` on each page, `@attribute [Authorize]`).

**Auth pages** (Login, Logout, Setup) are Razor Pages in `GUMS/Pages/Account/`, not Blazor components.

## UI Conventions

- **No Bootstrap JS** — all dropdowns, modals, and interactive elements must be Blazor-managed state (boolean flags toggling CSS classes)
- **Brand colours** defined as CSS variables in `wwwroot/app.css`: `--gg-primary-blue: #007BC4`, `--gg-dark-navy: #161B4E`, `--gg-error: #e50000`, `--gg-success: #26b050`
- **Font:** Poppins (Google Fonts)
- **NavLink bug:** Always add `Match="NavLinkMatch.All"` for top-level nav pages to prevent prefix matching
- **Modal pattern:** Use a `bool _showModal` flag + conditional `<div class="modal show d-block">` overlay with `rgba(0,0,0,0.5)` backdrop
- **Status badges:** Use `badge-gg-*` classes (e.g., `badge-gg-paid`, `badge-gg-pending`, `badge-gg-overdue`)

## Testing

Tests are in `GUMS.Tests/Services/` using xUnit + FluentAssertions + Moq. EF Core InMemory provider is used for database isolation. 9 test suites covering the core services.

# GUMS - Girlguiding Unit Management System

A desktop application for managing Girlguiding units, built with ASP.NET Core Blazor Server.

## Features

### Phase 1: Member Management
- Secure authentication with first-run setup
- Add and manage girls and leaders
- Multiple emergency contacts per member (supports split families)
- GDPR-compliant data removal with export capability
- Search and filter members

### Phase 2: Meetings & Attendance
- Configure school terms with dates and subscription amounts
- Plan regular weekly meetings with smart date suggestions
- Create special events (including multi-day camps) with costs and payment deadlines
- Track activities and consent requirements
- Quick attendance recording with bulk actions
- Attendance alerts for full-term absences and low attendance

### Phase 3: Payments & Accounts
- Termly subscription generation and tracking
- Activity payment tracking with partial payment support
- Overdue payment monitoring
- Double-entry accounting with chart of accounts
- Bank deposits and transaction journal
- Expense recording (direct and via expense claims)
- Expense claim workflow (submit, approve, reimburse)
- Expense account/category management
- Event financial summaries (income vs expenses per meeting)
- Event budgeting with per-girl, per-adult, and fixed cost types
- High/mid/low attendance scenario cost estimates
- Budget vs actual comparison with variance highlighting

### Reports
- Nights away tracking

### Coming Soon
- Phase 4: Communications and email list generation

## Requirements

- .NET 10.0 SDK or later
- Windows (for file-level database security)

## Getting Started

### Clone and Build

```bash
git clone <repository-url>
cd GUMS
dotnet build GUMS/GUMS.csproj
```

### Run the Application

```bash
dotnet run --project GUMS/GUMS.csproj
```

The application will start at `https://localhost:5001` (or the port shown in console).

### First Run

1. Navigate to the application URL in your browser
2. You'll be redirected to the **Setup** page
3. Create your admin account with a strong password
4. You're ready to start managing your unit!

## Running Tests

```bash
# Run all tests
dotnet test GUMS.Tests/GUMS.Tests.csproj

# Run with verbose output
dotnet test GUMS.Tests/GUMS.Tests.csproj --verbosity normal
```

Test suites cover: ConfigurationService, PersonService, TermService, MeetingService, AttendanceService, PaymentService, AccountingService.

## Project Structure

```
GUMS/
├── Data/
│   ├── Entities/          # Entity classes (16 entities)
│   ├── Enums/             # Enumerations (11 enums)
│   └── ApplicationDbContext.cs
├── Services/              # Business logic layer
│   ├── IConfigurationService.cs / ConfigurationService.cs
│   ├── IPersonService.cs / PersonService.cs
│   ├── ITermService.cs / TermService.cs
│   ├── IMeetingService.cs / MeetingService.cs
│   ├── IAttendanceService.cs / AttendanceService.cs
│   ├── IPaymentService.cs / PaymentService.cs
│   ├── IAccountingService.cs / AccountingService.cs
│   ├── IBudgetService.cs / BudgetService.cs
│   └── DatabaseSecurityService.cs
├── Components/
│   ├── Pages/             # Blazor pages
│   │   ├── Register/      # Member management (5 pages)
│   │   ├── Meetings/      # Meeting and attendance (8 pages)
│   │   ├── Payments/      # Payment tracking (5 pages)
│   │   ├── Accounts/      # Accounting and budgeting (11 pages)
│   │   ├── Reports/       # Nights away (1 page)
│   │   └── Configuration/ # Settings (2 pages)
│   ├── Layout/            # MainLayout, NavMenu
│   └── Shared/            # Reusable components (EmergencyContactEditor)
├── Pages/
│   └── Account/           # Razor Pages for auth (Login, Setup, Logout)
├── Migrations/            # EF Core migrations (6 migrations)
└── wwwroot/               # Static files, CSS, logo

GUMS.Tests/
└── Services/              # Unit tests for all services (7 test classes)
```

## Database

- **Location:** `%APPDATA%\GUMS\gums.db` (SQLite)
- **Security:** Windows file permissions restrict access to current user
- **Migrations:** Applied automatically on startup

### Manual Migration Commands

```bash
# Create a new migration
dotnet ef migrations add MigrationName --project GUMS

# Apply migrations manually
dotnet ef database update --project GUMS
```

## Configuration

### Unit Settings
Navigate to **Configuration > Unit Settings** to configure:
- Unit name and type (Rainbow, Brownie, Guide, Ranger)
- Default meeting day and times
- Default meeting location
- Subscription defaults

### Term Dates
Navigate to **Configuration > Term Dates** to:
- Add school terms with start/end dates
- Set subscription amounts per term
- View current, past, and future terms

## Branding

The application follows [Girlguiding Brand Guidelines](https://girlguiding.foleon.com/girlguiding-brand-guidelines/brand-guidelines/):
- Official brand colors (Navy #161B4E, Blue #007BC4, Cyan #44BDEE)
- Poppins font family
- Friendly, engaging tone of voice

### Customizing the Logo
Replace `wwwroot/UnitLogo.svg` with your unit's logo. See `wwwroot/README.md` for details.

## Documentation

| Document | Purpose |
|----------|---------|
| [SPECIFICATION.md](SPECIFICATION.md) | Original requirements |
| [PROGRESS.md](PROGRESS.md) | Implementation progress |
| [PHASE1_AUDIT.md](PHASE1_AUDIT.md) | Phase 1 completion audit |
| [PHASE2_AUDIT.md](PHASE2_AUDIT.md) | Phase 2 completion audit |
| [DATABASE_SECURITY.md](DATABASE_SECURITY.md) | Security implementation |

## Development

### Architecture
- **Blazor Server** with Interactive Server render mode
- **Entity Framework Core** with SQLite
- **ASP.NET Core Identity** for authentication
- **Service Layer Pattern** (IService/Service with DI)

### Adding a New Feature

1. Create/modify entities in `Data/Entities/`
2. Create service interface in `Services/IXxxService.cs`
3. Create service implementation in `Services/XxxService.cs`
4. Register in `Program.cs`: `builder.Services.AddScoped<IXxxService, XxxService>();`
5. Create UI pages in `Components/Pages/`
6. Add navigation in `NavMenu.razor`
7. Write tests in `GUMS.Tests/Services/`

### Code Style
- Async/await throughout
- Interface-based services
- Code-behind files for Blazor components (`.razor.cs`)
- FluentAssertions for tests

## License

Private - For Girlguiding unit use only.

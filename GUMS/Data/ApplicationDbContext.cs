using GUMS.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<Person> Persons { get; set; }
    public DbSet<EmergencyContact> EmergencyContacts { get; set; }
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<MeetingActivity> MeetingActivities { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Term> Terms { get; set; }
    public DbSet<UnitConfiguration> UnitConfigurations { get; set; }
    public DbSet<DataRemovalLog> DataRemovalLogs { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionLine> TransactionLines { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<ExpenseClaim> ExpenseClaims { get; set; }
    public DbSet<EventBudget> EventBudgets { get; set; }
    public DbSet<EventBudgetItem> EventBudgetItems { get; set; }
    public DbSet<EventBudgetIncome> EventBudgetIncomes { get; set; }
    public DbSet<UnitBudget> UnitBudgets { get; set; }
    public DbSet<UnitBudgetItem> UnitBudgetItems { get; set; }

    // Programme management DbSets
    public DbSet<BadgeDefinition> BadgeDefinitions { get; set; }
    public DbSet<BadgeClause> BadgeClauses { get; set; }
    public DbSet<UmaDefinition> UmaDefinitions { get; set; }
    public DbSet<ActivityCompletion> ActivityCompletions { get; set; }
    public DbSet<AwardTracking> AwardTrackings { get; set; }
    public DbSet<AwardedBadge> AwardedBadges { get; set; }
    public DbSet<AwardedThemeAward> AwardedThemeAwards { get; set; }
    public DbSet<NightsAwayBadge> NightsAwayBadges { get; set; }
    public DbSet<BankReconciliation> BankReconciliations { get; set; }

    // Inventory DbSets
    public DbSet<BadgeStockItem> BadgeStockItems { get; set; }
    public DbSet<BadgeStockTransaction> BadgeStockTransactions { get; set; }

    // Cost Centre DbSets
    public DbSet<CostCentre> CostCentres { get; set; }

    // Credit DbSets
    public DbSet<MemberCredit> MemberCredits { get; set; }
    public DbSet<CreditTransaction> CreditTransactions { get; set; }

    // Home Contact DbSets
    public DbSet<EventHomeContact> EventHomeContacts { get; set; }
    public DbSet<EventContactOverride> EventContactOverrides { get; set; }
    public DbSet<EventAdditionalPerson> EventAdditionalPeople { get; set; }

    // Patrol DbSets
    public DbSet<Patrol> Patrols { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Person configuration
        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.MembershipNumber).IsUnique();
            entity.HasIndex(p => p.IsActive);
            entity.HasIndex(p => p.IsDataRemoved);

            entity.Property(p => p.MembershipNumber).IsRequired();
            entity.Property(p => p.DateJoined).HasDefaultValueSql("datetime('now')");

            // EmergencyContacts cascade delete when Person is deleted
            entity.HasMany(p => p.EmergencyContacts)
                .WithOne(ec => ec.Person)
                .HasForeignKey(ec => ec.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            // Patrol membership
            entity.HasOne(p => p.Patrol)
                .WithMany(pa => pa.Members)
                .HasForeignKey(p => p.PatrolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.PatrolId);

            // At most one Leader and one Seconder per patrol
            entity.HasIndex(p => new { p.PatrolId, p.PatrolRole })
                .IsUnique()
                .HasFilter("\"PatrolRole\" <> 0");

            // A role-holder must belong to a patrol
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Person_PatrolRole_Requires_Patrol",
                "\"PatrolRole\" = 0 OR \"PatrolId\" IS NOT NULL"));
        });

        // Patrol configuration
        modelBuilder.Entity<Patrol>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.Section, p.Name }).IsUnique();
            entity.HasIndex(p => p.EmblemBadgeDefinitionId);

            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);

            entity.HasOne(p => p.EmblemBadge)
                .WithMany()
                .HasForeignKey(p => p.EmblemBadgeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // EmergencyContact configuration
        modelBuilder.Entity<EmergencyContact>(entity =>
        {
            entity.HasKey(ec => ec.Id);
            entity.HasIndex(ec => ec.PersonId);
        });

        // Meeting configuration
        modelBuilder.Entity<Meeting>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.Date);

            entity.HasMany(m => m.MeetingActivities)
                .WithOne(a => a.Meeting)
                .HasForeignKey(a => a.MeetingId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(m => m.Attendances)
                .WithOne(a => a.Meeting)
                .HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.CostCentre)
                .WithMany()
                .HasForeignKey(m => m.CostCentreId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // MeetingActivity configuration (replaces Activity)
        modelBuilder.Entity<MeetingActivity>(entity =>
        {
            entity.ToTable("Activities");
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.MeetingId);

            entity.HasOne(a => a.BadgeClause)
                .WithMany()
                .HasForeignKey(a => a.BadgeClauseId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.BadgeDefinition)
                .WithMany()
                .HasForeignKey(a => a.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.UmaDefinition)
                .WithMany()
                .HasForeignKey(a => a.UmaDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(a => a.Completions)
                .WithOne(c => c.MeetingActivity)
                .HasForeignKey(c => c.MeetingActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ActivityCompletion configuration
        modelBuilder.Entity<ActivityCompletion>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.MembershipNumber);
            entity.HasIndex(c => c.MeetingActivityId);
            entity.HasIndex(c => new { c.MeetingActivityId, c.MembershipNumber }).IsUnique();

            entity.Property(c => c.MembershipNumber).IsRequired();
        });

        // Attendance configuration
        // CRITICAL: Uses MembershipNumber (string) NOT PersonId FK
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.MembershipNumber);
            entity.HasIndex(a => a.MeetingId);

            // Unique constraint: one attendance record per member per meeting
            entity.HasIndex(a => new { a.MeetingId, a.MembershipNumber }).IsUnique();

            entity.Property(a => a.MembershipNumber).IsRequired();

            // NO foreign key to Person table - allows data to persist after Person removal
        });

        // BadgeDefinition configuration
        modelBuilder.Entity<BadgeDefinition>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => b.Theme);
            entity.HasIndex(b => b.Section);
            entity.HasIndex(b => b.BadgeType);

            entity.HasMany(b => b.Clauses)
                .WithOne(c => c.BadgeDefinition)
                .HasForeignKey(c => c.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BadgeClause configuration
        modelBuilder.Entity<BadgeClause>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.BadgeDefinitionId);
        });

        // UmaDefinition configuration
        modelBuilder.Entity<UmaDefinition>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Theme);
        });

        // AwardTracking configuration
        modelBuilder.Entity<AwardTracking>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.MembershipNumber).IsUnique();
            entity.Property(a => a.MembershipNumber).IsRequired();
        });

        // AwardedBadge configuration
        modelBuilder.Entity<AwardedBadge>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.MembershipNumber, a.BadgeDefinitionId }).IsUnique();

            entity.Property(a => a.MembershipNumber).IsRequired();

            entity.HasOne(a => a.BadgeDefinition)
                .WithMany()
                .HasForeignKey(a => a.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AwardedThemeAward configuration
        modelBuilder.Entity<AwardedThemeAward>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.MembershipNumber, a.Theme }).IsUnique();

            entity.Property(a => a.MembershipNumber).IsRequired();
        });

        // NightsAwayBadge configuration
        modelBuilder.Entity<NightsAwayBadge>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.MembershipNumber, a.Milestone }).IsUnique();

            entity.Property(a => a.MembershipNumber).IsRequired();
        });

        // Payment configuration
        // CRITICAL: Uses MembershipNumber (string) NOT PersonId FK
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.MembershipNumber);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.DueDate);

            entity.Property(p => p.MembershipNumber).IsRequired();
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.AmountPaid).HasPrecision(18, 2);
            entity.Property(p => p.RefundAmount).HasPrecision(18, 2);
            entity.Property(p => p.CreditApplied).HasPrecision(18, 2);

            // NO foreign key to Person table - allows data to persist after Person removal

            // Optional FKs to Meeting and Term
            entity.HasOne(p => p.Meeting)
                .WithMany()
                .HasForeignKey(p => p.MeetingId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(p => p.Term)
                .WithMany(t => t.Payments)
                .HasForeignKey(p => p.TermId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => p.IncomeAccountId);
            entity.HasOne(p => p.IncomeAccount)
                .WithMany()
                .HasForeignKey(p => p.IncomeAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(p => p.RefundTransaction)
                .WithMany()
                .HasForeignKey(p => p.RefundTransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Term configuration
        modelBuilder.Entity<Term>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => new { t.StartDate, t.EndDate });

            entity.Property(t => t.SubsAmount).HasPrecision(18, 2);
        });

        // UnitConfiguration configuration
        modelBuilder.Entity<UnitConfiguration>(entity =>
        {
            entity.HasKey(uc => uc.Id);
            entity.Property(uc => uc.DefaultSubsAmount).HasPrecision(18, 2);
            entity.Property(uc => uc.JoiningFeeAmount).HasPrecision(18, 2);
        });

        // DataRemovalLog configuration
        modelBuilder.Entity<DataRemovalLog>(entity =>
        {
            entity.HasKey(drl => drl.Id);
            entity.HasIndex(drl => drl.MembershipNumber);
            entity.HasIndex(drl => drl.RemovalDate);

            entity.Property(drl => drl.RemovalDate).HasDefaultValueSql("datetime('now')");
        });

        // Account configuration (Chart of Accounts)
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Code).IsUnique();
            entity.HasIndex(a => a.Type);

            entity.Property(a => a.Code).IsRequired();
            entity.Property(a => a.Name).IsRequired();
            entity.Property(a => a.Balance).HasPrecision(18, 2);
        });

        // Transaction configuration (Journal entries)
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Date);
            entity.HasIndex(t => t.PaymentId);

            entity.Property(t => t.Description).IsRequired();

            entity.HasOne(t => t.Payment)
                .WithMany()
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(t => t.Lines)
                .WithOne(tl => tl.Transaction)
                .HasForeignKey(tl => tl.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TransactionLine configuration (Debit/Credit entries)
        modelBuilder.Entity<TransactionLine>(entity =>
        {
            entity.HasKey(tl => tl.Id);
            entity.HasIndex(tl => tl.TransactionId);
            entity.HasIndex(tl => tl.AccountId);
            entity.HasIndex(tl => tl.BankReconciliationId);
            entity.HasIndex(tl => tl.CostCentreId);

            entity.Property(tl => tl.Debit).HasPrecision(18, 2);
            entity.Property(tl => tl.Credit).HasPrecision(18, 2);

            entity.HasOne(tl => tl.Account)
                .WithMany(a => a.TransactionLines)
                .HasForeignKey(tl => tl.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(tl => tl.CostCentre)
                .WithMany(cc => cc.TransactionLines)
                .HasForeignKey(tl => tl.CostCentreId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Expense configuration
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.ExpenseAccountId);
            entity.HasIndex(e => e.MeetingId);
            entity.HasIndex(e => e.CostCentreId);
            entity.HasIndex(e => e.ExpenseClaimId);

            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Description).IsRequired();

            entity.HasOne(e => e.ExpenseAccount)
                .WithMany()
                .HasForeignKey(e => e.ExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PaidFromAccount)
                .WithMany()
                .HasForeignKey(e => e.PaidFromAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CostCentre)
                .WithMany()
                .HasForeignKey(e => e.CostCentreId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Transaction)
                .WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ExpenseClaim)
                .WithMany(ec => ec.Expenses)
                .HasForeignKey(e => e.ExpenseClaimId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ExpenseClaim configuration
        modelBuilder.Entity<ExpenseClaim>(entity =>
        {
            entity.HasKey(ec => ec.Id);
            entity.HasIndex(ec => ec.Status);

            entity.Property(ec => ec.ClaimedBy).IsRequired();

            entity.HasOne(ec => ec.PaidFromAccount)
                .WithMany()
                .HasForeignKey(ec => ec.PaidFromAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ec => ec.Transaction)
                .WithMany()
                .HasForeignKey(ec => ec.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        // EventBudget configuration
        modelBuilder.Entity<EventBudget>(entity =>
        {
            entity.HasKey(eb => eb.Id);
            entity.HasIndex(eb => eb.MeetingId).IsUnique();

            entity.HasOne(eb => eb.Meeting)
                .WithMany()
                .HasForeignKey(eb => eb.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(eb => eb.Items)
                .WithOne(i => i.EventBudget)
                .HasForeignKey(i => i.EventBudgetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(eb => eb.IncomeItems)
                .WithOne(i => i.EventBudget)
                .HasForeignKey(i => i.EventBudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EventBudgetIncome configuration
        modelBuilder.Entity<EventBudgetIncome>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.EventBudgetId);

            entity.Property(i => i.Amount).HasPrecision(18, 2);
            entity.Property(i => i.Description).IsRequired();
        });

        // EventBudgetItem configuration
        modelBuilder.Entity<EventBudgetItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.EventBudgetId);

            entity.Property(i => i.Amount).HasPrecision(18, 2);
            entity.Property(i => i.Description).IsRequired();

            entity.HasOne(i => i.ExpenseAccount)
                .WithMany()
                .HasForeignKey(i => i.ExpenseAccountId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // UnitBudget configuration
        modelBuilder.Entity<UnitBudget>(entity =>
        {
            entity.HasKey(ub => ub.Id);
            entity.HasIndex(ub => ub.FinancialYearEnd).IsUnique();

            entity.HasMany(ub => ub.Items)
                .WithOne(i => i.UnitBudget)
                .HasForeignKey(i => i.UnitBudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UnitBudgetItem configuration
        modelBuilder.Entity<UnitBudgetItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.UnitBudgetId);

            entity.Property(i => i.Amount).HasPrecision(18, 2);
            entity.Property(i => i.Description).IsRequired();

            entity.HasOne(i => i.ExpenseAccount)
                .WithMany()
                .HasForeignKey(i => i.ExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BadgeStockItem configuration
        modelBuilder.Entity<BadgeStockItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.StockType);
            entity.HasIndex(i => i.BadgeDefinitionId);
            entity.HasIndex(i => i.IsActive);

            entity.Property(i => i.Name).IsRequired();
            entity.Property(i => i.UnitCost).HasPrecision(18, 2);

            entity.HasOne(i => i.BadgeDefinition)
                .WithMany()
                .HasForeignKey(i => i.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(i => i.Transactions)
                .WithOne(t => t.BadgeStockItem)
                .HasForeignKey(t => t.BadgeStockItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BadgeStockTransaction configuration
        modelBuilder.Entity<BadgeStockTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.BadgeStockItemId);
            entity.HasIndex(t => t.TransactionDate);
            entity.HasIndex(t => t.TransactionType);

            entity.Property(t => t.UnitCost).HasPrecision(18, 2);
            entity.Property(t => t.TotalCost).HasPrecision(18, 2);
        });

        // EventHomeContact configuration
        modelBuilder.Entity<EventHomeContact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MeetingId);

            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EventContactOverride configuration
        modelBuilder.Entity<EventContactOverride>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MeetingId);
            entity.HasIndex(e => new { e.MeetingId, e.MembershipNumber });

            entity.Property(e => e.MembershipNumber).IsRequired();

            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EventAdditionalPerson configuration
        modelBuilder.Entity<EventAdditionalPerson>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MeetingId);

            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BankReconciliation configuration
        modelBuilder.Entity<BankReconciliation>(entity =>
        {
            entity.HasKey(br => br.Id);
            entity.HasIndex(br => br.Status);
            entity.HasIndex(br => br.StatementDate);

            entity.Property(br => br.StatementBalance).HasPrecision(18, 2);
            entity.Property(br => br.ReconciledBookBalance).HasPrecision(18, 2);

            entity.HasMany(br => br.ReconciledLines)
                .WithOne(tl => tl.BankReconciliation)
                .HasForeignKey(tl => tl.BankReconciliationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CostCentre configuration
        modelBuilder.Entity<CostCentre>(entity =>
        {
            entity.HasKey(cc => cc.Id);
            entity.HasIndex(cc => cc.Name).IsUnique();
            entity.HasIndex(cc => cc.IsActive);

            entity.Property(cc => cc.Name).IsRequired().HasMaxLength(100);
        });

        // MemberCredit configuration
        modelBuilder.Entity<MemberCredit>(entity =>
        {
            entity.HasKey(mc => mc.Id);
            entity.HasIndex(mc => mc.MembershipNumber).IsUnique();

            entity.Property(mc => mc.MembershipNumber).IsRequired();
            entity.Property(mc => mc.Balance).HasPrecision(18, 2);
        });

        // CreditTransaction configuration
        modelBuilder.Entity<CreditTransaction>(entity =>
        {
            entity.HasKey(ct => ct.Id);
            entity.HasIndex(ct => ct.MembershipNumber);
            entity.HasIndex(ct => ct.Date);

            entity.Property(ct => ct.MembershipNumber).IsRequired();
            entity.Property(ct => ct.Amount).HasPrecision(18, 2);

            entity.HasOne(ct => ct.SourcePayment)
                .WithMany()
                .HasForeignKey(ct => ct.SourcePaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.TargetPayment)
                .WithMany()
                .HasForeignKey(ct => ct.TargetPaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.Transaction)
                .WithMany()
                .HasForeignKey(ct => ct.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

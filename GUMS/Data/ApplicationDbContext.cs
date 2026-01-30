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
    public DbSet<Activity> Activities { get; set; }
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

            entity.HasMany(m => m.Activities)
                .WithOne(a => a.Meeting)
                .HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(m => m.Attendances)
                .WithOne(a => a.Meeting)
                .HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Activity configuration
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.MeetingId);
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

            entity.Property(tl => tl.Debit).HasPrecision(18, 2);
            entity.Property(tl => tl.Credit).HasPrecision(18, 2);

            entity.HasOne(tl => tl.Account)
                .WithMany(a => a.TransactionLines)
                .HasForeignKey(tl => tl.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Expense configuration
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.ExpenseAccountId);
            entity.HasIndex(e => e.MeetingId);
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
    }
}

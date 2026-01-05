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
    }
}

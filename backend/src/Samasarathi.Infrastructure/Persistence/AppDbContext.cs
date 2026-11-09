using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Samasarathi.Domain;
using Samasarathi.Domain.Entities;

namespace Samasarathi.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<VerificationCheck> VerificationChecks => Set<VerificationCheck>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<Rider> Riders => Set<Rider>();
    public DbSet<Ride> Rides => Set<Ride>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("postgis");

        b.Entity<Driver>(e =>
        {
            e.HasIndex(d => d.Phone).IsUnique();
            e.Property(d => d.FullName).HasMaxLength(120);
            e.Property(d => d.Phone).HasMaxLength(15);
            e.Property(d => d.RatePerKm).HasPrecision(8, 2);
            e.Property(d => d.VerificationStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.ReviewedBy).HasMaxLength(15);
            e.Property(d => d.RejectionReason).HasMaxLength(500);
            e.HasIndex(d => d.VerificationStatus);
            e.Property(d => d.LastKnownLocation).HasColumnType("geography (point, 4326)");
            e.HasOne(d => d.Vehicle).WithOne().HasForeignKey<Vehicle>(v => v.DriverId);
            e.HasMany(d => d.Subscriptions).WithOne().HasForeignKey(s => s.DriverId);
            e.HasMany(d => d.VerificationChecks).WithOne().HasForeignKey(v => v.DriverId);
            e.HasMany(d => d.Documents).WithOne().HasForeignKey(doc => doc.DriverId);
        });

        b.Entity<DriverDocument>(e =>
        {
            e.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.StorageKey).HasMaxLength(100);
            e.Property(d => d.OriginalFileName).HasMaxLength(255);
            e.Property(d => d.ContentType).HasMaxLength(50);
            e.HasIndex(d => new { d.DriverId, d.Type }).IsUnique();
        });

        b.Entity<Vehicle>(e =>
        {
            e.HasIndex(v => v.RegistrationNumber).IsUnique();
            e.Property(v => v.RegistrationNumber).HasMaxLength(15);
            e.Property(v => v.Type).HasConversion<string>().HasMaxLength(10);
        });

        b.Entity<Subscription>(e =>
        {
            e.Property(s => s.MonthlyPrice).HasPrecision(10, 2);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.PlanName).HasMaxLength(60);
            e.Property(s => s.Note).HasMaxLength(200);
            e.Property(s => s.GrantedBy).HasMaxLength(15);
            e.HasIndex(s => new { s.DriverId, s.EndsAt });
        });

        b.Entity<VerificationCheck>(e =>
        {
            e.Property(v => v.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(v => v.Result).HasConversion<string>().HasMaxLength(10);
            e.Property(v => v.Notes).HasMaxLength(500);
        });

        b.Entity<Rider>(e => e.HasIndex(r => r.Phone).IsUnique());

        b.Entity<Ride>(e =>
        {
            e.HasOne<Rider>().WithMany().HasForeignKey(r => r.RiderId);
            e.HasOne<Driver>().WithMany().HasForeignKey(r => r.DriverId);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.PricingMode).HasConversion<string>().HasMaxLength(10);
            e.Property(r => r.Pickup).HasColumnType("geography (point, 4326)");
            e.Property(r => r.Drop).HasColumnType("geography (point, 4326)");
            foreach (var p in new[] { "RatePerKm", "EstimatedDistanceKm", "EstimatedMinutes", "EstimatedFare",
                                      "ActualDistanceKm", "ActualMinutes", "FinalFare" })
                e.Property(p).HasPrecision(10, 2);
            e.Property(r => r.CancelReason).HasMaxLength(100);
            e.Property(r => r.CommentForDriver).HasMaxLength(Ride.MaxCommentLength);
            e.Property(r => r.CommentForRider).HasMaxLength(Ride.MaxCommentLength);
            e.HasIndex(r => new { r.RiderId, r.RequestedAt });
            e.HasIndex(r => new { r.DriverId, r.Status });

            // Database-level guard against double booking: one active ride per rider and per driver, even if two
            // requests arrive at the same instant.
            const string active = "\"Status\" IN ('Requested', 'Accepted', 'DriverArrived', 'InProgress')";
            e.HasIndex(r => r.RiderId).IsUnique().HasFilter(active).HasDatabaseName("IX_Rides_OneActivePerRider");
            e.HasIndex(r => r.DriverId).IsUnique().HasFilter(active).HasDatabaseName("IX_Rides_OneActivePerDriver");
        });

        // Ids are created in C# (Guid.NewGuid()), not by the database. Telling EF this means a new
        // child added to a loaded parent (e.g. driver.Documents.Add) is inserted, not treated as an update.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            if (entity.FindProperty("Id") is { ClrType: var t } id && t == typeof(Guid))
                id.ValueGenerated = ValueGenerated.Never;
        }
    }
}

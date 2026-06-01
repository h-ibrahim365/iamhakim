using IAmHakim.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IAmHakim.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SiteStat> SiteStats => Set<SiteStat>();

    public DbSet<SiteEvent> SiteEvents => Set<SiteEvent>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SiteStat>(entity =>
        {
            entity.HasKey(stat => stat.Id);
            entity.Property(stat => stat.Id).ValueGeneratedNever();
            entity.Property(stat => stat.TotalVisits).IsRequired();
            entity.Property(stat => stat.UpClicks).IsRequired();
            entity.Property(stat => stat.Clicks).IsRequired();
            entity.Property(stat => stat.AlgoRuns).IsRequired();
            entity.Property(stat => stat.CreatedAtUtc).IsRequired();
            entity.Property(stat => stat.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<SiteEvent>(entity =>
        {
            entity.HasKey(siteEvent => siteEvent.Id);
            entity.Property(siteEvent => siteEvent.Kind).HasMaxLength(64).IsRequired();
            entity.Property(siteEvent => siteEvent.Label).HasMaxLength(220).IsRequired();
            entity.Property(siteEvent => siteEvent.CreatedAtUtc).IsRequired();
            entity.HasIndex(siteEvent => siteEvent.CreatedAtUtc);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(booking => booking.Id);
            entity.Property(booking => booking.Id).HasMaxLength(64);
            entity.Property(booking => booking.ManageToken).HasMaxLength(128).IsRequired();
            entity.Property(booking => booking.DecisionToken).HasMaxLength(128);
            entity.Property(booking => booking.Name).HasMaxLength(120).IsRequired();
            entity.Property(booking => booking.Email).HasMaxLength(200).IsRequired();
            entity.Property(booking => booking.IpHash).HasMaxLength(128).IsRequired();
            entity.Property(booking => booking.Message).HasMaxLength(2000);
            entity.Property(booking => booking.MeetingLocation).HasMaxLength(300);
            entity.Property(booking => booking.Language).HasMaxLength(8).IsRequired();
            entity.Property(booking => booking.Provider).HasMaxLength(16);
            entity.Property(booking => booking.Status).HasMaxLength(24).IsRequired();
            entity.HasIndex(booking => booking.ManageToken).IsUnique();
            entity.HasIndex(booking => booking.DecisionToken).IsUnique();
            entity.HasIndex(booking => booking.StartUtc);
            entity.HasIndex(booking => booking.RequestedStartUtc);
            entity.HasIndex(booking => new { booking.Email, booking.Status, booking.CreatedAtUtc });
            entity.HasIndex(booking => new { booking.IpHash, booking.Status, booking.CreatedAtUtc });
        });

        modelBuilder.Entity<EmailVerification>(entity =>
        {
            entity.HasKey(verification => verification.Id);
            entity.Property(verification => verification.Id).HasMaxLength(64);
            entity.Property(verification => verification.Email).HasMaxLength(200).IsRequired();
            entity.Property(verification => verification.IpHash).HasMaxLength(128).IsRequired();
            entity.Property(verification => verification.CodeHash).HasMaxLength(128).IsRequired();
            entity.Property(verification => verification.VerificationToken).HasMaxLength(128);
            entity.Property(verification => verification.Attempts).IsRequired();
            entity.Property(verification => verification.CreatedAtUtc).IsRequired();
            entity.Property(verification => verification.ExpiresAtUtc).IsRequired();
            entity.HasIndex(verification => verification.Email);
            entity.HasIndex(verification => new { verification.IpHash, verification.CreatedAtUtc });
            entity.HasIndex(verification => verification.VerificationToken).IsUnique();
            entity.HasIndex(verification => verification.ExpiresAtUtc);
        });
    }
}

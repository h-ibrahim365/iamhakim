using IAmHakim.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IAmHakim.Api.Services;

public sealed class BookingRetentionOptions
{
    /// <summary>How long after a meeting ends the booking record is retained, in days.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>How long old e-mail verification rows are retained, in days.</summary>
    public int EmailVerificationRetentionDays { get; set; } = 7;

    /// <summary>How often the retention sweep runs, in hours.</summary>
    public int SweepIntervalHours { get; set; } = 24;
}

/// <summary>
/// Daily sweep that deletes booking rows past the retention horizon promised in
/// the privacy policy. Also removes stale e-mail verification rows so short-lived
/// anti-abuse IP hashes do not live longer than needed. Logs only counts — no PII
/// ever reaches the log stream.
/// </summary>
public sealed class BookingRetentionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<BookingRetentionOptions> options,
    ILogger<BookingRetentionService> logger) : BackgroundService
{
    private readonly BookingRetentionOptions opt = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial 30s delay so the app is fully up (migrations applied, DB warm)
        // before we hit it with delete queries.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RetentionSweepResult result = await SweepOnceAsync(stoppingToken);
                if (result.DeletedBookings > 0 || result.DeletedEmailVerifications > 0)
                {
                    logger.LogInformation(
                        "Retention sweep deleted {BookingCount} booking rows and {VerificationCount} email verification rows.",
                        result.DeletedBookings,
                        result.DeletedEmailVerifications);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Booking retention sweep failed.");
            }

            try { await Task.Delay(TimeSpan.FromHours(opt.SweepIntervalHours), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task<RetentionSweepResult> SweepOnceAsync(CancellationToken ct)
    {
        DateTimeOffset bookingCutoff = DateTimeOffset.UtcNow.AddDays(-opt.RetentionDays);
        DateTimeOffset verificationCutoff = DateTimeOffset.UtcNow.AddDays(-opt.EmailVerificationRetentionDays);
        await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);

        int deletedBookings = await db.Bookings
            .Where(b => b.EndUtc < bookingCutoff)
            .ExecuteDeleteAsync(ct);

        int deletedEmailVerifications = await db.EmailVerifications
            .Where(v => v.CreatedAtUtc < verificationCutoff)
            .ExecuteDeleteAsync(ct);

        return new RetentionSweepResult(deletedBookings, deletedEmailVerifications);
    }

    private sealed record RetentionSweepResult(int DeletedBookings, int DeletedEmailVerifications);
}

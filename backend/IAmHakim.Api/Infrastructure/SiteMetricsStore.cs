using IAmHakim.Api.Contracts;
using System.Collections.Concurrent;

namespace IAmHakim.Api.Infrastructure;

/// <summary>
/// Stores the first live counters of the website.
/// This is intentionally in-memory for the first MVP. Replace it with PostgreSQL later.
/// </summary>
public sealed class SiteMetricsStore
{
    private readonly ConcurrentDictionary<DateOnly, long> dailyVisits = new();
    private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

    private long totalVisits;
    private long upClicks;
    private DateTimeOffset? lastVisitAtUtc;
    private DateTimeOffset? lastUpAtUtc;

    /// <summary>
    /// Returns the current API health state.
    /// </summary>
    public HealthResponse GetHealth()
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

        return new HealthResponse(
            Status: "connected",
            Api: "up",
            Storage: "in-memory",
            Bot: "not-configured",
            StartedAtUtc: this.startedAtUtc,
            CheckedAtUtc: nowUtc,
            UptimeSeconds: GetUptimeSeconds(nowUtc));
    }

    /// <summary>
    /// Records one page visit and returns the updated public counters.
    /// </summary>
    public SiteStatsResponse RecordVisit()
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        DateOnly todayUtc = DateOnly.FromDateTime(nowUtc.UtcDateTime);

        Interlocked.Increment(ref this.totalVisits);
        this.dailyVisits.AddOrUpdate(todayUtc, 1, (_, currentValue) => currentValue + 1);
        this.lastVisitAtUtc = nowUtc;

        return this.GetStats(nowUtc);
    }

    /// <summary>
    /// Records one UP click and returns the updated public counters.
    /// </summary>
    public SiteStatsResponse RecordUpClick()
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

        Interlocked.Increment(ref this.upClicks);
        this.lastUpAtUtc = nowUtc;

        return this.GetStats(nowUtc);
    }

    /// <summary>
    /// Returns the current public counters without mutating them.
    /// </summary>
    public SiteStatsResponse GetStats()
    {
        return this.GetStats(DateTimeOffset.UtcNow);
    }

    private SiteStatsResponse GetStats(DateTimeOffset nowUtc)
    {
        DateOnly todayUtc = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        this.dailyVisits.TryGetValue(todayUtc, out long todayVisits);

        return new SiteStatsResponse(
            TotalVisits: Interlocked.Read(ref this.totalVisits),
            TodayVisits: todayVisits,
            UpClicks: Interlocked.Read(ref this.upClicks),
            LastVisitAtUtc: this.lastVisitAtUtc,
            LastUpAtUtc: this.lastUpAtUtc,
            StartedAtUtc: this.startedAtUtc,
            UptimeSeconds: GetUptimeSeconds(nowUtc));
    }

    private long GetUptimeSeconds(DateTimeOffset nowUtc)
    {
        return Math.Max(0, (long)(nowUtc - this.startedAtUtc).TotalSeconds);
    }
}

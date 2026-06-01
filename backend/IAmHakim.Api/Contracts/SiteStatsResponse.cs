namespace IAmHakim.Api.Contracts;

/// <summary>
/// Represents the public counters displayed on iamhakim.com.
/// </summary>
public sealed record SiteStatsResponse(
    long TotalVisits,
    long TodayVisits,
    long UpClicks,
    DateTimeOffset? LastVisitAtUtc,
    DateTimeOffset? LastUpAtUtc,
    DateTimeOffset StartedAtUtc,
    long UptimeSeconds);

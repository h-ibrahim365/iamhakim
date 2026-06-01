namespace IAmHakim.Api.Models;

public sealed class SiteStat
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public long TotalVisits { get; set; }

    public long UpClicks { get; set; }

    public long Clicks { get; set; }

    public long AlgoRuns { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? LastVisitAtUtc { get; set; }

    public DateTimeOffset? LastUpAtUtc { get; set; }
}

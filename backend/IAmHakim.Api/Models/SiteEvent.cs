namespace IAmHakim.Api.Models;

public sealed class SiteEvent
{
    public long Id { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

namespace IAmHakim.Api.Contracts;

/// <summary>
/// Represents the public health state exposed to the Angular frontend.
/// </summary>
public sealed record HealthResponse(
    string Status,
    string Api,
    string Storage,
    string Bot,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CheckedAtUtc,
    long UptimeSeconds);

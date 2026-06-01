namespace IAmHakim.Api.Models;

public sealed record HealthResponse(
    string Status,
    string Api,
    string Database,
    string Realtime,
    int LiveClients,
    long UptimeSeconds,
    long LatencyMs,
    DateTimeOffset ServerTimeUtc);

public sealed record StatsResponse(
    long TotalVisits,
    long UpClicks,
    long Clicks,
    long AlgoRuns,
    int LiveClients,
    DateTimeOffset? LastVisitAtUtc,
    DateTimeOffset? LastUpAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SiteEventResponse(
    long Id,
    string Kind,
    string Label,
    DateTimeOffset CreatedAtUtc);

public sealed record UpResponse(
    string Message,
    StatsResponse Stats);

public sealed record FlowSimulationResponse(
    string CorrelationId,
    string Message,
    DateTimeOffset CreatedAtUtc);

public sealed record AlgoRunRequest(
    string? Outcome,
    int? Expanded,
    bool? Maze);
public sealed record AddressSuggestionResponse(
    string Label,
    string? Latitude,
    string? Longitude);

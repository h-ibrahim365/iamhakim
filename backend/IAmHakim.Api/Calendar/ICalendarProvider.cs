using IAmHakim.Api.Models;

namespace IAmHakim.Api.Calendar;

public sealed record BusyInterval(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>
/// A calendar backend the owner controls. Implementations: Google Calendar and
/// Microsoft Graph (Outlook). Multiple providers can be combined to compute a
/// merged free/busy view, and one "primary" provider receives created events.
/// </summary>
public interface ICalendarProvider
{
    /// <summary>"google" | "graph" | "mock".</summary>
    string Name { get; }

    /// <summary>True if this provider also creates/updates/deletes events (the primary one).</summary>
    bool IsPrimary { get; }

    Task<IReadOnlyList<BusyInterval>> GetBusyAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>Creates the event in the owner's calendar. Returns the provider event id.</summary>
    Task<string> CreateEventAsync(Booking booking, CancellationToken ct);

    Task UpdateEventAsync(Booking booking, CancellationToken ct);

    Task DeleteEventAsync(string calendarEventId, CancellationToken ct);
}

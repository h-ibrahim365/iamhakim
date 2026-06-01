using IAmHakim.Api.Models;
using System.Collections.Concurrent;

namespace IAmHakim.Api.Calendar;

/// <summary>
/// In-memory calendar used when Booking:Mode = "mock" (default in dev).
/// Lets the whole booking flow work end-to-end with no Google/Graph credentials.
/// Seeds a couple of fake busy blocks so the availability grid looks realistic.
/// </summary>
public sealed class MockCalendarProvider : ICalendarProvider
{
    public string Name => "mock";
    public bool IsPrimary => true;

    private readonly ConcurrentDictionary<string, BusyInterval> events = new();
    private readonly List<BusyInterval> seededBusy;

    public MockCalendarProvider()
    {
        // seed: "busy" tomorrow afternoon and in three days' morning
        DateTimeOffset baseDay = DateTimeOffset.UtcNow.Date;
        seededBusy = new List<BusyInterval>
        {
            new(baseDay.AddDays(1).AddHours(14), baseDay.AddDays(1).AddHours(16)),
            new(baseDay.AddDays(3).AddHours(9), baseDay.AddDays(3).AddHours(11))
        };
    }

    public Task<IReadOnlyList<BusyInterval>> GetBusyAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        IReadOnlyList<BusyInterval> busy = seededBusy
            .Concat(events.Values)
            .Where(b => b.EndUtc > fromUtc && b.StartUtc < toUtc)
            .ToList();
        return Task.FromResult(busy);
    }

    public Task<string> CreateEventAsync(Booking booking, CancellationToken ct)
    {
        string id = $"mock-{Guid.NewGuid():N}";
        events[id] = new BusyInterval(booking.StartUtc, booking.EndUtc);
        return Task.FromResult(id);
    }

    public Task UpdateEventAsync(Booking booking, CancellationToken ct)
    {
        if (booking.CalendarEventId is { } id && events.ContainsKey(id))
        {
            events[id] = new BusyInterval(booking.StartUtc, booking.EndUtc);
        }
        return Task.CompletedTask;
    }

    public Task DeleteEventAsync(string calendarEventId, CancellationToken ct)
    {
        events.TryRemove(calendarEventId, out _);
        return Task.CompletedTask;
    }
}

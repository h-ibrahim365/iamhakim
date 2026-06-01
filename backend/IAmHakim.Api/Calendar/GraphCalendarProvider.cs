using IAmHakim.Api.Models;
using Microsoft.Extensions.Options;

namespace IAmHakim.Api.Calendar;

/// <summary>
/// Microsoft Graph (Outlook) provider. Reads free/busy via getSchedule and creates
/// events in the owner's calendar using app-only auth (client credentials).
///
/// TO ENABLE:
///   1. Add NuGet: Microsoft.Graph + Azure.Identity
///   2. Register an Entra app with Calendars.ReadWrite (application) + admin consent
///   3. Configure "Booking:Graph": TenantId, ClientId, ClientSecret, OwnerUpn
///   4. Replace the NotConfigured throws with the SDK calls shown in comments.
/// </summary>
public sealed class GraphCalendarProvider : ICalendarProvider
{
    private readonly GraphOptions options;

    public GraphCalendarProvider(IOptions<BookingOptions> bookingOptions)
    {
        options = bookingOptions.Value.Graph;
    }

    public string Name => "graph";
    public bool IsPrimary => options.IsPrimary;

    public Task<IReadOnlyList<BusyInterval>> GetBusyAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        EnsureConfigured();
        // var client = BuildGraphClient(); // ClientSecretCredential
        // var body = new Microsoft.Graph.Users.Item.Calendar.GetSchedule.GetSchedulePostRequestBody {
        //     Schedules = new() { options.OwnerUpn },
        //     StartTime = new DateTimeTimeZone { DateTime = fromUtc.UtcDateTime.ToString("o"), TimeZone = "UTC" },
        //     EndTime   = new DateTimeTimeZone { DateTime = toUtc.UtcDateTime.ToString("o"),  TimeZone = "UTC" },
        //     AvailabilityViewInterval = 30
        // };
        // var res = await client.Users[options.OwnerUpn].Calendar.GetSchedule.PostAsGetSchedulePostResponseAsync(body, cancellationToken: ct);
        // map res.Value[0].ScheduleItems where Status == Busy -> BusyInterval
        throw new CalendarNotConfiguredException("graph");
    }

    public Task<string> CreateEventAsync(Booking booking, CancellationToken ct)
    {
        EnsureConfigured();
        // var ev = new Event {
        //     Subject = $"{booking.Kind} · {booking.Name}",
        //     Body = new ItemBody { ContentType = BodyType.Text, Content = booking.Message ?? "" },
        //     Start = new DateTimeTimeZone { DateTime = booking.StartUtc.UtcDateTime.ToString("o"), TimeZone = "UTC" },
        //     End   = new DateTimeTimeZone { DateTime = booking.EndUtc.UtcDateTime.ToString("o"),   TimeZone = "UTC" },
        //     Attendees = new() { new Attendee { EmailAddress = new EmailAddress { Address = booking.Email, Name = booking.Name } } }
        // };
        // var created = await client.Users[options.OwnerUpn].Events.PostAsync(ev, cancellationToken: ct);
        // return created!.Id!;
        throw new CalendarNotConfiguredException("graph");
    }

    public Task UpdateEventAsync(Booking booking, CancellationToken ct)
    {
        EnsureConfigured();
        throw new CalendarNotConfiguredException("graph");
    }

    public Task DeleteEventAsync(string calendarEventId, CancellationToken ct)
    {
        EnsureConfigured();
        // await client.Users[options.OwnerUpn].Events[calendarEventId].DeleteAsync(cancellationToken: ct);
        throw new CalendarNotConfiguredException("graph");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.OwnerUpn))
        {
            throw new CalendarNotConfiguredException("graph");
        }
    }
}

public sealed class CalendarNotConfiguredException(string provider)
    : Exception($"Calendar provider '{provider}' is selected but not configured. See Booking options.");

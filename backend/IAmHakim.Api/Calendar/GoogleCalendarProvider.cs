using System.Globalization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using IAmHakim.Api.Models;
using Microsoft.Extensions.Options;

namespace IAmHakim.Api.Calendar;

/// <summary>
/// Google Calendar provider using OAuth refresh token authentication.
/// It reads busy intervals from Google Calendar and creates, updates, or deletes
/// booking events in the configured primary calendar.
/// </summary>
public sealed class GoogleCalendarProvider : ICalendarProvider
{
    private static readonly string[] Scopes =
    [
        CalendarService.Scope.CalendarEvents,
        CalendarService.Scope.CalendarFreebusy
    ];

    private readonly BookingOptions bookingOptions;
    private readonly GoogleOptions options;

    public GoogleCalendarProvider(IOptions<BookingOptions> bookingOptions)
    {
        this.bookingOptions = bookingOptions.Value;
        options = this.bookingOptions.Google;
    }

    public string Name => "google";

    public bool IsPrimary => options.IsPrimary;

    public async Task<IReadOnlyList<BusyInterval>> GetBusyAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        EnsureConfigured();

        CalendarService service = BuildService();

        FreeBusyRequest request = new()
        {
            TimeMinRaw = ToGoogleUtc(fromUtc),
            TimeMaxRaw = ToGoogleUtc(toUtc),
            TimeZone = "UTC",
            Items =
            [
                new FreeBusyRequestItem
                {
                    Id = options.CalendarId
                }
            ]
        };

        FreeBusyResponse response = await service.Freebusy.Query(request).ExecuteAsync(ct);

        if (response.Calendars is null ||
            !response.Calendars.TryGetValue(options.CalendarId, out FreeBusyCalendar? calendar) ||
            calendar.Busy is null)
        {
            return [];
        }

        return calendar.Busy
            .Where(period => !string.IsNullOrWhiteSpace(period.StartRaw) && !string.IsNullOrWhiteSpace(period.EndRaw))
            .Select(period => new BusyInterval(
                ParseGoogleUtc(period.StartRaw),
                ParseGoogleUtc(period.EndRaw)))
            .ToList();
    }

    public async Task<string> CreateEventAsync(Booking booking, CancellationToken ct)
    {
        EnsureConfigured();

        CalendarService service = BuildService();
        Event googleEvent = ToGoogleEvent(booking);

        EventsResource.InsertRequest insert = service.Events.Insert(googleEvent, options.CalendarId);
        insert.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;

        Event created = await insert.ExecuteAsync(ct);

        if (string.IsNullOrWhiteSpace(created.Id))
        {
            throw new InvalidOperationException("Google Calendar created an event without returning an id.");
        }

        return created.Id;
    }

    public async Task UpdateEventAsync(Booking booking, CancellationToken ct)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(booking.CalendarEventId))
        {
            throw new InvalidOperationException("Cannot update a Google Calendar event without an event id.");
        }

        CalendarService service = BuildService();
        Event googleEvent = ToGoogleEvent(booking);

        EventsResource.UpdateRequest update = service.Events.Update(googleEvent, options.CalendarId, booking.CalendarEventId);
        update.SendUpdates = EventsResource.UpdateRequest.SendUpdatesEnum.All;

        await update.ExecuteAsync(ct);
    }

    public async Task DeleteEventAsync(string calendarEventId, CancellationToken ct)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(calendarEventId))
        {
            return;
        }

        CalendarService service = BuildService();

        EventsResource.DeleteRequest delete = service.Events.Delete(options.CalendarId, calendarEventId);
        delete.SendUpdates = EventsResource.DeleteRequest.SendUpdatesEnum.All;

        await delete.ExecuteAsync(ct);
    }

    private CalendarService BuildService()
    {
        GoogleAuthorizationCodeFlow flow = new(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret
            },
            Scopes = Scopes
        });

        TokenResponse token = new()
        {
            RefreshToken = options.RefreshToken
        };

        UserCredential credential = new(flow, options.OwnerEmail, token);

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "IAmHakim"
        });
    }

    private Event ToGoogleEvent(Booking booking)
    {
        return new Event
        {
            Summary = $"IAmHakim booking - {booking.Name}",
            Description = BuildDescription(booking),
            Location = BuildLocation(booking),
            Start = new EventDateTime
            {
                DateTimeRaw = ToGoogleUtc(booking.StartUtc),
                TimeZone = "UTC"
            },
            End = new EventDateTime
            {
                DateTimeRaw = ToGoogleUtc(booking.EndUtc),
                TimeZone = "UTC"
            },
            Attendees =
            [
                new EventAttendee
                {
                    Email = booking.Email,
                    DisplayName = booking.Name
                }
            ],
            Transparency = "opaque"
        };
    }

    private string BuildDescription(Booking booking)
    {
        string message = string.IsNullOrWhiteSpace(booking.Message)
            ? "No message."
            : booking.Message.Trim();

        return
            $"Booked from iamhakim.com\n\n" +
            $"Name: {booking.Name}\n" +
            $"Email: {booking.Email}\n" +
            $"Kind: {booking.Kind}\n" +
            BuildLogisticsDescription(booking) +
            $"\nMessage:\n{message}";
    }

    private string BuildLogisticsDescription(Booking booking)
    {
        BookingMeetingOptions meeting = bookingOptions.Meeting;

        return booking.Kind switch
        {
            MeetingKind.Call when !string.IsNullOrWhiteSpace(meeting.OwnerPhoneNumber) =>
                $"Phone: {meeting.OwnerPhoneNumber.Trim()}\n",
            MeetingKind.Call =>
                "Phone: to be confirmed by email.\n",
            MeetingKind.InPerson when !string.IsNullOrWhiteSpace(booking.MeetingLocation) && !string.IsNullOrWhiteSpace(meeting.OwnerPhoneNumber) =>
                $"Meeting address: {booking.MeetingLocation.Trim()}\nPhone if needed: {meeting.OwnerPhoneNumber.Trim()}\n",
            MeetingKind.InPerson when !string.IsNullOrWhiteSpace(booking.MeetingLocation) =>
                $"Meeting address: {booking.MeetingLocation.Trim()}\n",
            _ => string.Empty
        };
    }

    private string BuildLocation(Booking booking)
    {
        BookingMeetingOptions meeting = bookingOptions.Meeting;

        return booking.Kind switch
        {
            MeetingKind.Call when !string.IsNullOrWhiteSpace(meeting.OwnerPhoneNumber) => $"Phone: {meeting.OwnerPhoneNumber.Trim()}",
            MeetingKind.Call => "Phone call",
            MeetingKind.InPerson when !string.IsNullOrWhiteSpace(booking.MeetingLocation) => booking.MeetingLocation.Trim(),
            MeetingKind.InPerson => "In-person meeting",
            _ => string.Empty
        };
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.CalendarId) ||
            string.IsNullOrWhiteSpace(options.OwnerEmail) ||
            string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.RefreshToken))
        {
            throw new CalendarNotConfiguredException("google");
        }
    }

    private static string ToGoogleUtc(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseGoogleUtc(string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}

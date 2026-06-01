namespace IAmHakim.Api.Models;

/// <summary>
/// A booking request persisted locally. The visitor first creates a pending request;
/// the owner then accepts or rejects it manually. A calendar event is created only
/// after acceptance.
/// </summary>
public sealed class Booking
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Secret handed to the visitor; the only thing needed to manage the request.</summary>
    public string ManageToken { get; set; } = string.Empty;

    /// <summary>Secret sent only to the owner by e-mail to open the accept/reject page.</summary>
    public string? DecisionToken { get; set; }

    /// <summary>Id of the created event in the owner's primary calendar (Google or Graph).</summary>
    public string? CalendarEventId { get; set; }

    /// <summary>Which provider holds the event ("google" | "graph" | "mock"). Empty until accepted.</summary>
    public string Provider { get; set; } = string.Empty;

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    /// <summary>Requested new start when a confirmed booking is waiting for reschedule approval.</summary>
    public DateTimeOffset? RequestedStartUtc { get; set; }

    /// <summary>Requested new end when a confirmed booking is waiting for reschedule approval.</summary>
    public DateTimeOffset? RequestedEndUtc { get; set; }

    public MeetingKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Salted hash of the requester IP, used only for anti-abuse limits.</summary>
    public string IpHash { get; set; } = string.Empty;

    public string? Message { get; set; }

    /// <summary>Meeting address entered by the visitor when the meeting is in person.</summary>
    public string? MeetingLocation { get; set; }

    /// <summary>Visitor language captured at request time: en | fr | nl.</summary>
    public string Language { get; set; } = "en";

    /// <summary>pending | accepted | reschedule_requested | rejected | cancelled | expired.</summary>
    public string Status { get; set; } = BookingStatuses.Pending;

    /// <summary>When a pending request stops blocking the selected slot.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class BookingStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string RescheduleRequested = "reschedule_requested";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";

    // Legacy values kept blocking-safe for rows created by older versions.
    public const string Confirmed = "confirmed";
    public const string Rescheduled = "rescheduled";

    public static readonly string[] Blocking =
    [
        Pending,
        Accepted,
        RescheduleRequested,
        Confirmed,
        Rescheduled
    ];
}

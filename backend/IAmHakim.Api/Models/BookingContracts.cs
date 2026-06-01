namespace IAmHakim.Api.Models;

/// <summary>A bookable time slot exposed to the public availability calendar.</summary>
public sealed record AvailabilitySlot(
    string Id,            // opaque slot id, e.g. "2026-06-03T18:00:00Z"
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool Available);

/// <summary>A day grouping of slots, for the calendar UI.</summary>
public sealed record AvailabilityDay(
    DateOnly Date,
    IReadOnlyList<AvailabilitySlot> Slots);

public sealed record AvailabilityResponse(
    DateOnly FromDate,
    DateOnly ToDate,
    string TimeZone,
    IReadOnlyList<AvailabilityDay> Days);

public enum MeetingKind
{
    Video,
    Call,
    InPerson
}

/// <summary>Public booking request - no account required.</summary>
public sealed record BookingRequest(
    string SlotId,
    string Name,
    string Email,
    string Message,
    MeetingKind Kind,
    string? MeetingLocation,
    string EmailVerificationToken,
    string? Language = null);

/// <summary>
/// Returned after a booking request was stored. ManageToken lets the visitor
/// cancel the request later without creating an account - it is the only visitor secret.
/// </summary>
public sealed record BookingResponse(
    string BookingId,
    string ManageToken,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    MeetingKind Kind,
    string Status);          // "pending" | "accepted" | "rejected" | "cancelled" | "expired"

public sealed record ManageBookingRequest(
    string ManageToken,
    string Action,           // "cancel" | "reschedule"
    string? NewSlotId);

public sealed record AdminManageBookingRequest(
    string Token,
    string Action,           // "cancel" | "reschedule"
    string? NewSlotId);

public sealed record BookingView(
    string BookingId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    MeetingKind Kind,
    string Status,
    string Name,
    string Email,
    string? Message,
    string? MeetingLocation,
    DateTimeOffset? RequestedStartUtc,
    DateTimeOffset? RequestedEndUtc);

public sealed record BookingDecisionView(
    string BookingId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    MeetingKind Kind,
    string Status,
    string Name,
    string Email,
    string? Message,
    string? MeetingLocation,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RequestedStartUtc,
    DateTimeOffset? RequestedEndUtc);

public sealed record EmailVerificationRequest(
    string Email,
    string? Language = null,
    string? TurnstileToken = null);

public sealed record EmailVerificationResponse(
    string VerificationId,
    DateTimeOffset ExpiresAtUtc);

public sealed record EmailVerificationConfirmRequest(
    string VerificationId,
    string Email,
    string Code);

public sealed record EmailVerificationConfirmResponse(
    string Email,
    string EmailVerificationToken);

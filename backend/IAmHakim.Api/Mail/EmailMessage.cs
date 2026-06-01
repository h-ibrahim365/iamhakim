namespace IAmHakim.Api.Mail;

/// <summary>
/// Small transaction-email envelope used by the booking flow.
/// The concrete sender decides how to map it to its provider API.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string HtmlBody,
    string? ReplyTo = null);

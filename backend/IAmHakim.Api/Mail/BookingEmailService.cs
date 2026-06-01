using System.Globalization;
using System.Net;
using IAmHakim.Api.Calendar;
using IAmHakim.Api.Models;
using Microsoft.Extensions.Options;

namespace IAmHakim.Api.Mail;

/// <summary>Builds and sends all transaction e-mails for the booking request workflow.</summary>
public sealed class BookingEmailService(
    IEmailSender sender,
    IOptions<BookingOptions> bookingOptions,
    IOptions<MailOptions> mailOptions)
{
    private const string AdminLanguage = "fr";

    private readonly BookingOptions bookingOpt = bookingOptions.Value;
    private readonly MailOptions mailOpt = mailOptions.Value;

    public async Task SendRequestCreatedAsync(Booking booking, string manageUrl, string decisionUrl, CancellationToken ct)
    {
        await SendVisitorRequestReceivedAsync(booking, manageUrl, ct);
        await SendAdminRequestReceivedAsync(booking, decisionUrl, ct);
    }

    public Task SendEmailVerificationCodeAsync(string email, string code, DateTimeOffset expiresAtUtc, string? language, CancellationToken ct)
    {
        string lang = NormalizeLanguage(language);
        DateTimeOffset localExpiry = TimeZoneInfo.ConvertTime(expiresAtUtc, ResolveBookingTimeZone());
        string expires = localExpiry.ToString("HH:mm", CultureFor(lang));

        string text =
            $"{T(lang, "verification.subject")}\n\n" +
            $"{T(lang, "verification.codeLine")} {code}\n" +
            $"{string.Format(CultureInfo.InvariantCulture, T(lang, "verification.expiryLine"), expires)}\n\n" +
            $"{T(lang, "verification.ignore")}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "verification.title"),
            T(lang, "verification.eyebrow"),
            T(lang, "verification.heading"),
            string.Format(CultureInfo.InvariantCulture, T(lang, "verification.bodyHtml"), H(expires)),
            CodeBlock(code),
            Note(T(lang, "verification.ignore")));

        return sender.SendAsync(new EmailMessage(
            To: email,
            Subject: T(lang, "verification.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    public Task SendAcceptedAsync(Booking booking, string manageUrl, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string kind = FormatKind(booking.Kind, lang);
        string calendarUrl = BuildGoogleCalendarUrl(booking, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{T(lang, "accepted.textIntro")}\n\n" +
            $"{T(lang, "field.date")} : {when}\n" +
            $"{T(lang, "field.type")} : {kind}\n" +
            $"{T(lang, "field.topic")} : {booking.Message}\n" +
            MeetingLogisticsText(booking, lang) +
            $"\n{T(lang, "button.google")} : {calendarUrl}\n" +
            $"{T(lang, "button.manageCancel")} : {manageUrl}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "accepted.title"),
            T(lang, "accepted.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "accepted.bodyHtml"), H(when)),
            HighlightCard(T(lang, "accepted.cardTitle"), T(lang, "accepted.cardText")),
            BookingDetails(booking, when, lang),
            MeetingLogisticsCard(booking, lang),
            BookingActionButtons(booking, calendarUrl, manageUrl, lang),
            Note(T(lang, "reply.note")));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "accepted.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    public Task SendRescheduleAcceptedAsync(Booking booking, string manageUrl, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string kind = FormatKind(booking.Kind, lang);
        string calendarUrl = BuildGoogleCalendarUrl(booking, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{T(lang, "rescheduleAccepted.textIntro")}\n\n" +
            $"{T(lang, "field.newDate")} : {when}\n" +
            $"{T(lang, "field.type")} : {kind}\n" +
            $"{T(lang, "field.topic")} : {booking.Message}\n" +
            MeetingLogisticsText(booking, lang) +
            $"\n{T(lang, "button.google")} : {calendarUrl}\n" +
            $"{T(lang, "button.manageCancel")} : {manageUrl}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "rescheduleAccepted.title"),
            T(lang, "rescheduleAccepted.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "rescheduleAccepted.bodyHtml"), H(when)),
            HighlightCard(T(lang, "rescheduleAccepted.cardTitle"), T(lang, "rescheduleAccepted.cardText")),
            BookingDetails(booking, when, lang),
            MeetingLogisticsCard(booking, lang),
            BookingActionButtons(booking, calendarUrl, manageUrl, lang));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "rescheduleAccepted.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    public Task SendRejectedAsync(Booking booking, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{string.Format(CultureInfo.InvariantCulture, T(lang, "rejected.textIntro"), when)}\n" +
            $"{T(lang, "rejected.textBody")}\n\n" +
            $"{T(lang, "reply.offerAnother")}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "rejected.title"),
            T(lang, "generic.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "rejected.bodyHtml"), H(when)),
            HighlightCard(T(lang, "rejected.cardTitle"), T(lang, "rejected.cardText")),
            BookingDetails(booking, when, lang),
            Note(T(lang, "reply.offerAnother")));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "rejected.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    public Task SendRescheduleRejectedAsync(Booking booking, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{T(lang, "rescheduleRejected.textIntro")}\n" +
            $"{string.Format(CultureInfo.InvariantCulture, T(lang, "rescheduleRejected.textBody"), when)}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "rescheduleRejected.title"),
            T(lang, "generic.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "rescheduleRejected.bodyHtml"), H(when)),
            BookingDetails(booking, when, lang));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "rescheduleRejected.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    public Task SendCancelledByVisitorAsync(Booking booking, CancellationToken ct)
    {
        List<Task> tasks = [SendVisitorCancellationConfirmationAsync(booking, ct)];

        if (!string.IsNullOrWhiteSpace(mailOpt.AdminEmail))
        {
            tasks.Add(SendAdminCancellationNoticeAsync(booking, "Le visiteur a annulé la demande ou le rendez-vous.", ct));
        }

        return Task.WhenAll(tasks);
    }

    public Task SendCancelledByOwnerAsync(Booking booking, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{string.Format(CultureInfo.InvariantCulture, T(lang, "ownerCancelled.textIntro"), when)}\n" +
            $"{T(lang, "reply.offerAnother")}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "ownerCancelled.title"),
            T(lang, "generic.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "ownerCancelled.bodyHtml"), H(when)),
            HighlightCard(T(lang, "cancelled.cardTitle"), T(lang, "reply.offerAnother")));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "ownerCancelled.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    public Task SendRescheduledByOwnerAsync(Booking booking, string manageUrl, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string calendarUrl = BuildGoogleCalendarUrl(booking, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{T(lang, "ownerRescheduled.textIntro")}\n\n" +
            $"{T(lang, "field.newDate")} : {when}\n" +
            MeetingLogisticsText(booking, lang) +
            $"\n{T(lang, "button.manageCancel")} : {manageUrl}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "ownerRescheduled.title"),
            T(lang, "generic.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "ownerRescheduled.bodyHtml"), H(when)),
            BookingDetails(booking, when, lang),
            MeetingLogisticsCard(booking, lang),
            BookingActionButtons(booking, calendarUrl, manageUrl, lang));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "ownerRescheduled.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    public async Task SendRescheduleRequestedAsync(Booking booking, string manageUrl, string decisionUrl, bool keepsCurrentUntilAccepted, CancellationToken ct)
    {
        await SendVisitorRescheduleRequestReceivedAsync(booking, manageUrl, keepsCurrentUntilAccepted, ct);
        await SendAdminRescheduleRequestReceivedAsync(booking, decisionUrl, keepsCurrentUntilAccepted, ct);
    }

    private Task SendVisitorCancellationConfirmationAsync(Booking booking, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{string.Format(CultureInfo.InvariantCulture, T(lang, "visitorCancelled.textIntro"), when)}\n" +
            $"{T(lang, "visitorCancelled.textBody")}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "visitorCancelled.title"),
            T(lang, "generic.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "visitorCancelled.bodyHtml"), H(when)),
            HighlightCard(T(lang, "cancelled.cardTitle"), T(lang, "visitorCancelled.cardText")),
            Note(T(lang, "reply.offerAnother")));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "visitorCancelled.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    private Task SendAdminCancellationNoticeAsync(Booking booking, string title, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mailOpt.AdminEmail)) return Task.CompletedTask;

        string when = FormatWhen(booking.StartUtc, AdminLanguage);
        string text =
            $"{title}\n\n" +
            $"Nom : {booking.Name}\n" +
            $"E-mail : {booking.Email}\n" +
            $"Date : {when}\n" +
            $"Statut : {booking.Status}";

        string html = Layout(
            "Demande annulée",
            "Booking update",
            title,
            RequestSummaryCard(booking.Name, booking.Email, when, FormatKind(booking.Kind, AdminLanguage), booking.Message ?? string.Empty),
            HighlightCard("Créneau libéré", "Le créneau est à nouveau disponible côté booking."));

        return sender.SendAsync(new EmailMessage(
            To: mailOpt.AdminEmail,
            Subject: $"Demande annulée - {booking.Name}",
            TextBody: text,
            HtmlBody: html,
            ReplyTo: booking.Email), ct);
    }

    private Task SendVisitorRequestReceivedAsync(Booking booking, string manageUrl, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string when = FormatWhen(booking.StartUtc, lang);
        string kind = FormatKind(booking.Kind, lang);
        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{T(lang, "requestReceived.textIntro")}\n\n" +
            $"{T(lang, "field.date")} : {when}\n" +
            $"{T(lang, "field.type")} : {kind}\n" +
            $"{T(lang, "field.topic")} : {booking.Message}\n" +
            MeetingLogisticsText(booking, lang) +
            $"\n{T(lang, "requestReceived.pendingLine")}\n" +
            $"{T(lang, "button.manageCancelRequest")} : {manageUrl}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        string html = Layout(
            T(lang, "requestReceived.title"),
            T(lang, "requestReceived.eyebrow"),
            H(Greeting(lang, booking.Name)),
            T(lang, "requestReceived.textIntro"),
            HighlightCard(T(lang, "requestReceived.cardTitle"), T(lang, "requestReceived.cardText")),
            BookingDetails(booking, when, lang),
            MeetingLogisticsCard(booking, lang),
            ButtonRow(SecondaryButton(manageUrl, T(lang, "button.manageCancelRequest"))));

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "requestReceived.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    private Task SendAdminRequestReceivedAsync(Booking booking, string decisionUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mailOpt.AdminEmail)) return Task.CompletedTask;

        string when = FormatWhen(booking.StartUtc, AdminLanguage);
        string text =
            $"Nouvelle demande de rendez-vous.\n\n" +
            $"Nom : {booking.Name}\n" +
            $"E-mail : {booking.Email}\n" +
            $"Date : {when}\n" +
            $"Type : {FormatKind(booking.Kind, AdminLanguage)}\n" +
            MeetingLogisticsText(booking, AdminLanguage) +
            $"Sujet : {booking.Message}\n" +
            $"Langue visiteur : {DisplayLanguage(booking.Language)}\n\n" +
            $"Voir la demande : {decisionUrl}";

        string html = Layout(
            "Nouvelle demande",
            "Action requise",
            "Une demande vérifiée attend ta décision.",
            RequestSummaryCard(booking.Name, booking.Email, when, FormatKind(booking.Kind, AdminLanguage), booking.Message ?? string.Empty),
            DetailsTable(new (string Key, string Value)[]
            {
                ("Logistique", MeetingLogisticsSummary(booking, AdminLanguage)),
                ("Langue visiteur", DisplayLanguage(booking.Language))
            }),
            ButtonRow(Button(decisionUrl, "Voir la demande")));

        return sender.SendAsync(new EmailMessage(
            To: mailOpt.AdminEmail,
            Subject: $"Nouvelle demande à valider - {booking.Name}",
            TextBody: text,
            HtmlBody: html,
            ReplyTo: booking.Email), ct);
    }

    private Task SendVisitorRescheduleRequestReceivedAsync(Booking booking, string manageUrl, bool keepsCurrentUntilAccepted, CancellationToken ct)
    {
        string lang = BookingLanguage(booking);
        string requestedWhen = booking.RequestedStartUtc is { } requested ? FormatWhen(requested, lang) : FormatWhen(booking.StartUtc, lang);
        string currentWhen = FormatWhen(booking.StartUtc, lang);
        string note = keepsCurrentUntilAccepted
            ? string.Format(CultureInfo.InvariantCulture, T(lang, "rescheduleRequested.keepCurrent"), currentWhen)
            : T(lang, "rescheduleRequested.pendingOnly");

        string html = Layout(
            T(lang, "rescheduleRequested.title"),
            T(lang, "rescheduleRequested.eyebrow"),
            H(Greeting(lang, booking.Name)),
            string.Format(CultureInfo.InvariantCulture, T(lang, "rescheduleRequested.bodyHtml"), H(requestedWhen)),
            HighlightCard(T(lang, "requestReceived.cardTitle"), note),
            ButtonRow(SecondaryButton(manageUrl, T(lang, "button.manageCancelRequest"))));

        string text =
            $"{Greeting(lang, booking.Name)}\n\n" +
            $"{string.Format(CultureInfo.InvariantCulture, T(lang, "rescheduleRequested.textIntro"), requestedWhen)}\n" +
            $"{note}\n\n" +
            $"{T(lang, "button.manageCancel")} : {manageUrl}\n\n" +
            $"Hakim\n" +
            $"iamhakim.com";

        return sender.SendAsync(new EmailMessage(
            To: booking.Email,
            Subject: T(lang, "rescheduleRequested.subject"),
            TextBody: text,
            HtmlBody: html,
            ReplyTo: mailOpt.ReplyToEmail), ct);
    }

    private Task SendAdminRescheduleRequestReceivedAsync(Booking booking, string decisionUrl, bool keepsCurrentUntilAccepted, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mailOpt.AdminEmail)) return Task.CompletedTask;

        string currentWhen = FormatWhen(booking.StartUtc, AdminLanguage);
        string requestedWhen = booking.RequestedStartUtc is { } requested ? FormatWhen(requested, AdminLanguage) : currentWhen;
        string intro = keepsCurrentUntilAccepted
            ? "Le recruteur demande un changement d’horaire. L’ancien créneau reste actif tant que tu n’acceptes pas."
            : "Le recruteur a modifié une demande encore en attente.";

        string html = Layout(
            "Changement demandé",
            "Action requise",
            intro,
            RequestSummaryCard(booking.Name, booking.Email, currentWhen, FormatKind(booking.Kind, AdminLanguage), booking.Message ?? string.Empty),
            DetailsTable(new (string Key, string Value)[]
            {
                ("Horaire actuel", currentWhen),
                ("Horaire demandé", requestedWhen),
                ("Logistique", MeetingLogisticsSummary(booking, AdminLanguage)),
                ("Statut", booking.Status),
                ("Langue visiteur", DisplayLanguage(booking.Language))
            }),
            ButtonRow(Button(decisionUrl, "Voir la demande")));

        string text =
            $"Changement d’horaire demandé.\n\n" +
            $"Nom : {booking.Name}\n" +
            $"E-mail : {booking.Email}\n" +
            $"Horaire actuel : {currentWhen}\n" +
            $"Horaire demandé : {requestedWhen}\n" +
            MeetingLogisticsText(booking, AdminLanguage) +
            $"Langue visiteur : {DisplayLanguage(booking.Language)}\n\n" +
            $"Voir la demande : {decisionUrl}";

        return sender.SendAsync(new EmailMessage(
            To: mailOpt.AdminEmail,
            Subject: $"Changement d’horaire à valider - {booking.Name}",
            TextBody: text,
            HtmlBody: html,
            ReplyTo: booking.Email), ct);
    }

    private string FormatWhen(DateTimeOffset utc, string? language)
    {
        string lang = NormalizeLanguage(language);
        TimeZoneInfo tz = ResolveBookingTimeZone();
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, tz);
        CultureInfo culture = CultureFor(lang);
        string pattern = lang switch
        {
            "fr" => "dddd d MMMM yyyy 'à' HH:mm",
            "nl" => "dddd d MMMM yyyy 'om' HH:mm",
            _ => "dddd d MMMM yyyy 'at' HH:mm"
        };
        string formatted = local.ToString(pattern, culture);
        return CapitalizeFirst(formatted, culture);
    }

    private TimeZoneInfo ResolveBookingTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(bookingOpt.TimeZone); }
        catch { return TimeZoneInfo.Utc; }
    }

    private string BuildGoogleCalendarUrl(Booking booking, string? language)
    {
        string lang = NormalizeLanguage(language);
        string start = booking.StartUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string end = booking.EndUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string title = string.Format(CultureInfo.InvariantCulture, T(lang, "calendar.title"), FormatKind(booking.Kind, lang));
        string details =
            $"{T(lang, "calendar.detailsIntro")}\n\n" +
            $"{T(lang, "field.type")} : {FormatKind(booking.Kind, lang)}\n" +
            MeetingLogisticsText(booking, lang) +
            $"{T(lang, "field.topic")} : {booking.Message}\n";

        string location = CalendarLocation(booking, lang);

        return "https://calendar.google.com/calendar/render?action=TEMPLATE" +
            $"&text={U(title)}" +
            $"&dates={U($"{start}/{end}")}" +
            $"&details={U(details)}" +
            $"&location={U(location)}" +
            $"&ctz={U(bookingOpt.TimeZone)}";
    }

    private static string BookingLanguage(Booking booking) => NormalizeLanguage(booking.Language);

    public static string NormalizeLanguage(string? language)
    {
        string normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "fr" or "fr-be" or "fr-fr" => "fr",
            "nl" or "nl-be" or "nl-nl" => "nl",
            _ => "en"
        };
    }

    private static CultureInfo CultureFor(string? language)
    {
        return NormalizeLanguage(language) switch
        {
            "fr" => CultureInfo.GetCultureInfo("fr-BE"),
            "nl" => CultureInfo.GetCultureInfo("nl-BE"),
            _ => CultureInfo.GetCultureInfo("en-GB")
        };
    }

    private static string CapitalizeFirst(string value, CultureInfo culture)
    {
        return value.Length == 0
            ? value
            : value[0].ToString(culture).ToUpper(culture) + value[1..];
    }

    private static string DisplayLanguage(string? language)
    {
        return NormalizeLanguage(language) switch
        {
            "fr" => "Français",
            "nl" => "Néerlandais",
            _ => "Anglais"
        };
    }

    private static string Greeting(string lang, string name)
    {
        return NormalizeLanguage(lang) switch
        {
            "fr" => $"Bonjour {name},",
            "nl" => $"Hallo {name},",
            _ => $"Hi {name},"
        };
    }

    private static string FormatKind(MeetingKind kind, string? language)
    {
        string lang = NormalizeLanguage(language);
        return kind switch
        {
            MeetingKind.Video => T(lang, "kind.video"),
            MeetingKind.Call => T(lang, "kind.call"),
            MeetingKind.InPerson => T(lang, "kind.inperson"),
            _ => kind.ToString()
        };
    }

    private string BookingDetails(Booking booking, string when, string? language)
    {
        string lang = NormalizeLanguage(language);
        return DetailsTable(new (string Key, string Value)[]
        {
            (T(lang, "field.date"), when),
            (T(lang, "field.type"), FormatKind(booking.Kind, lang)),
            (T(lang, "field.topic"), booking.Message ?? string.Empty)
        });
    }

    private string MeetingLogisticsText(Booking booking, string? language)
    {
        string summary = MeetingLogisticsSummary(booking, language);
        if (string.IsNullOrWhiteSpace(summary)) return string.Empty;

        string lang = NormalizeLanguage(language);
        return $"{T(lang, "field.logistics")} : {summary}\n";
    }

    private string MeetingLogisticsCard(Booking booking, string? language)
    {
        string summary = MeetingLogisticsSummary(booking, language);
        if (string.IsNullOrWhiteSpace(summary)) return string.Empty;

        string lang = NormalizeLanguage(language);
        return HighlightCard(T(lang, "logistics.cardTitle"), summary);
    }

    private string MeetingLogisticsSummary(Booking booking, string? language)
    {
        string lang = NormalizeLanguage(language);
        string phone = bookingOpt.Meeting.OwnerPhoneNumber.Trim();

        return booking.Kind switch
        {
            MeetingKind.Call when !string.IsNullOrWhiteSpace(phone) =>
                string.Format(CultureInfo.InvariantCulture, T(lang, "logistics.call.withPhone"), phone),
            MeetingKind.Call =>
                T(lang, "logistics.call.pending"),
            MeetingKind.InPerson when !string.IsNullOrWhiteSpace(booking.MeetingLocation) && !string.IsNullOrWhiteSpace(phone) =>
                string.Format(CultureInfo.InvariantCulture, T(lang, "logistics.inperson.withPhone"), booking.MeetingLocation, phone),
            MeetingKind.InPerson when !string.IsNullOrWhiteSpace(booking.MeetingLocation) =>
                string.Format(CultureInfo.InvariantCulture, T(lang, "logistics.inperson.addressOnly"), booking.MeetingLocation),
            _ => string.Empty
        };
    }

    private string CalendarLocation(Booking booking, string? language)
    {
        string lang = NormalizeLanguage(language);
        string phone = bookingOpt.Meeting.OwnerPhoneNumber.Trim();

        return booking.Kind switch
        {
            MeetingKind.Video => T(lang, "calendar.location.video"),
            MeetingKind.Call when !string.IsNullOrWhiteSpace(phone) => phone,
            MeetingKind.Call => T(lang, "calendar.location.call"),
            MeetingKind.InPerson when !string.IsNullOrWhiteSpace(booking.MeetingLocation) => booking.MeetingLocation,
            MeetingKind.InPerson => T(lang, "calendar.location.inperson"),
            _ => string.Empty
        };
    }

    private string BookingActionButtons(Booking booking, string calendarUrl, string manageUrl, string? language)
    {
        string lang = NormalizeLanguage(language);
        return ButtonRow(
            Button(calendarUrl, T(lang, "button.google")),
            SecondaryButton(manageUrl, T(lang, "button.manageCancel")));
    }

    private static string T(string language, string key)
    {
        string lang = NormalizeLanguage(language);
        return (lang, key) switch
        {
            ("fr", "verification.subject") => "Code de vérification - Hakim",
            ("fr", "verification.title") => "Code de vérification",
            ("fr", "verification.eyebrow") => "Sécurité booking",
            ("fr", "verification.heading") => "Confirme ton adresse e-mail",
            ("fr", "verification.codeLine") => "Voici ton code pour confirmer ton adresse e-mail :",
            ("fr", "verification.expiryLine") => "Il expire à {0}.",
            ("fr", "verification.bodyHtml") => "Utilise ce code pour finaliser ta demande de rendez-vous. Il expire à <strong>{0}</strong>.",
            ("fr", "verification.ignore") => "Si tu n’es pas à l’origine de cette demande, tu peux simplement ignorer cet e-mail.",

            ("fr", "accepted.subject") => "Rendez-vous confirmé - Hakim",
            ("fr", "accepted.title") => "Rendez-vous confirmé",
            ("fr", "accepted.eyebrow") => "Booking confirmé",
            ("fr", "accepted.textIntro") => "Ta demande de rendez-vous est confirmée.",
            ("fr", "accepted.bodyHtml") => "C’est confirmé pour <strong>{0}</strong>.",
            ("fr", "accepted.cardTitle") => "Rendez-vous validé",
            ("fr", "accepted.cardText") => "Le créneau est confirmé. Tu peux l’ajouter à ton agenda ou conserver cet e-mail comme référence.",

            ("fr", "rescheduleAccepted.subject") => "Horaire confirmé - Hakim",
            ("fr", "rescheduleAccepted.title") => "Horaire mis à jour",
            ("fr", "rescheduleAccepted.eyebrow") => "Reschedule confirmé",
            ("fr", "rescheduleAccepted.textIntro") => "Le changement d’horaire est confirmé.",
            ("fr", "rescheduleAccepted.bodyHtml") => "Le rendez-vous est maintenant confirmé pour <strong>{0}</strong>.",
            ("fr", "rescheduleAccepted.cardTitle") => "Nouvel horaire validé",
            ("fr", "rescheduleAccepted.cardText") => "L’ancien créneau est remplacé par ce nouvel horaire.",

            ("fr", "rejected.subject") => "Demande de rendez-vous - Hakim",
            ("fr", "rejected.title") => "Créneau non confirmé",
            ("fr", "rejected.textIntro") => "Merci pour ta demande de rendez-vous pour {0}.",
            ("fr", "rejected.textBody") => "Je ne pourrai malheureusement pas confirmer ce créneau.",
            ("fr", "rejected.bodyHtml") => "Merci pour ta demande de rendez-vous pour <strong>{0}</strong>.",
            ("fr", "rejected.cardTitle") => "Créneau libéré",
            ("fr", "rejected.cardText") => "Je ne pourrai malheureusement pas confirmer ce créneau.",

            ("fr", "rescheduleRejected.subject") => "Changement d’horaire - Hakim",
            ("fr", "rescheduleRejected.title") => "Changement non confirmé",
            ("fr", "rescheduleRejected.textIntro") => "Je ne peux pas valider le changement d’horaire demandé.",
            ("fr", "rescheduleRejected.textBody") => "Le rendez-vous initial reste donc confirmé pour {0}.",
            ("fr", "rescheduleRejected.bodyHtml") => "Je ne peux pas valider le changement d’horaire demandé. Le rendez-vous initial reste confirmé pour <strong>{0}</strong>.",

            ("fr", "ownerCancelled.subject") => "Rendez-vous annulé - Hakim",
            ("fr", "ownerCancelled.title") => "Rendez-vous annulé",
            ("fr", "ownerCancelled.textIntro") => "Le rendez-vous prévu pour {0} a été annulé.",
            ("fr", "ownerCancelled.bodyHtml") => "Le rendez-vous prévu pour <strong>{0}</strong> a été annulé.",

            ("fr", "ownerRescheduled.subject") => "Nouvel horaire de rendez-vous - Hakim",
            ("fr", "ownerRescheduled.title") => "Rendez-vous déplacé",
            ("fr", "ownerRescheduled.textIntro") => "Le rendez-vous a été déplacé.",
            ("fr", "ownerRescheduled.bodyHtml") => "Le rendez-vous a été déplacé au <strong>{0}</strong>.",

            ("fr", "visitorCancelled.subject") => "Demande de rendez-vous annulée - Hakim",
            ("fr", "visitorCancelled.title") => "Demande annulée",
            ("fr", "visitorCancelled.textIntro") => "Ta demande de rendez-vous pour {0} a bien été annulée.",
            ("fr", "visitorCancelled.textBody") => "Le créneau est libéré.",
            ("fr", "visitorCancelled.bodyHtml") => "Ta demande de rendez-vous pour <strong>{0}</strong> a bien été annulée.",
            ("fr", "visitorCancelled.cardText") => "Aucune action supplémentaire n’est nécessaire.",

            ("fr", "requestReceived.subject") => "Demande de rendez-vous reçue - Hakim",
            ("fr", "requestReceived.title") => "Demande reçue",
            ("fr", "requestReceived.eyebrow") => "Booking request",
            ("fr", "requestReceived.textIntro") => "J’ai bien reçu ta demande de rendez-vous.",
            ("fr", "requestReceived.pendingLine") => "Le créneau n’est pas encore confirmé. Je valide les demandes manuellement et je te réponds par e-mail.",
            ("fr", "requestReceived.cardTitle") => "En attente de validation",
            ("fr", "requestReceived.cardText") => "Le créneau n’est pas encore confirmé. Je valide les demandes manuellement et je te réponds par e-mail.",

            ("fr", "rescheduleRequested.subject") => "Changement d’horaire demandé - Hakim",
            ("fr", "rescheduleRequested.title") => "Changement demandé",
            ("fr", "rescheduleRequested.eyebrow") => "Reschedule request",
            ("fr", "rescheduleRequested.textIntro") => "J’ai bien reçu ta demande de changement vers {0}.",
            ("fr", "rescheduleRequested.bodyHtml") => "J’ai bien reçu ta demande de changement vers <strong>{0}</strong>.",
            ("fr", "rescheduleRequested.keepCurrent") => "Le rendez-vous actuel reste confirmé pour {0} tant que le nouvel horaire n’est pas accepté.",
            ("fr", "rescheduleRequested.pendingOnly") => "La demande reste en attente de validation sur le nouvel horaire.",

            ("fr", "generic.eyebrow") => "Booking update",
            ("fr", "cancelled.cardTitle") => "Créneau libéré",
            ("fr", "reply.note") => "Si tu dois préciser quelque chose, tu peux répondre directement à cet e-mail.",
            ("fr", "reply.offerAnother") => "Tu peux répondre à cet e-mail si tu veux proposer un autre moment.",
            ("fr", "button.google") => "Ajouter à Google Calendar",
            ("fr", "button.manageCancel") => "Gérer / annuler",
            ("fr", "button.manageCancelRequest") => "Gérer / annuler la demande",
            ("fr", "field.date") => "Date",
            ("fr", "field.newDate") => "Nouvelle date",
            ("fr", "field.type") => "Type",
            ("fr", "field.topic") => "Sujet",
            ("fr", "field.logistics") => "Logistique",
            ("fr", "logistics.cardTitle") => "Informations pratiques",
            ("fr", "logistics.call.withPhone") => "Tu peux me contacter au {0} à l’heure indiquée.",
            ("fr", "logistics.call.pending") => "Le numéro d’appel sera confirmé par e-mail.",
            ("fr", "logistics.inperson.withPhone") => "Adresse de rencontre : {0}. Si besoin, tu peux me contacter au {1} à l’heure indiquée.",
            ("fr", "logistics.inperson.addressOnly") => "Adresse de rencontre : {0}.",
            ("fr", "kind.video") => "Visio",
            ("fr", "kind.call") => "Appel",
            ("fr", "kind.inperson") => "En personne",
            ("fr", "calendar.title") => "Rendez-vous avec Hakim - {0}",
            ("fr", "calendar.detailsIntro") => "Rendez-vous confirmé via iamhakim.com",
            ("fr", "calendar.location.video") => "Lien visio à confirmer",
            ("fr", "calendar.location.call") => "Appel téléphonique",
            ("fr", "calendar.location.inperson") => "Lieu à confirmer",

            ("nl", "verification.subject") => "Verificatiecode - Hakim",
            ("nl", "verification.title") => "Verificatiecode",
            ("nl", "verification.eyebrow") => "Bookingbeveiliging",
            ("nl", "verification.heading") => "Bevestig je e-mailadres",
            ("nl", "verification.codeLine") => "Dit is je code om je e-mailadres te bevestigen:",
            ("nl", "verification.expiryLine") => "Hij verloopt om {0}.",
            ("nl", "verification.bodyHtml") => "Gebruik deze code om je afspraakaanvraag af te ronden. Hij verloopt om <strong>{0}</strong>.",
            ("nl", "verification.ignore") => "Als jij deze aanvraag niet hebt gestart, mag je deze e-mail negeren.",

            ("nl", "accepted.subject") => "Afspraak bevestigd - Hakim",
            ("nl", "accepted.title") => "Afspraak bevestigd",
            ("nl", "accepted.eyebrow") => "Booking bevestigd",
            ("nl", "accepted.textIntro") => "Je afspraakaanvraag is bevestigd.",
            ("nl", "accepted.bodyHtml") => "Bevestigd voor <strong>{0}</strong>.",
            ("nl", "accepted.cardTitle") => "Afspraak goedgekeurd",
            ("nl", "accepted.cardText") => "Het tijdslot is bevestigd. Je kunt het aan je agenda toevoegen of deze e-mail bewaren als referentie.",

            ("nl", "rescheduleAccepted.subject") => "Nieuw tijdstip bevestigd - Hakim",
            ("nl", "rescheduleAccepted.title") => "Tijdstip bijgewerkt",
            ("nl", "rescheduleAccepted.eyebrow") => "Reschedule bevestigd",
            ("nl", "rescheduleAccepted.textIntro") => "De wijziging van het tijdstip is bevestigd.",
            ("nl", "rescheduleAccepted.bodyHtml") => "De afspraak is nu bevestigd voor <strong>{0}</strong>.",
            ("nl", "rescheduleAccepted.cardTitle") => "Nieuw tijdstip goedgekeurd",
            ("nl", "rescheduleAccepted.cardText") => "Het oude tijdslot wordt vervangen door dit nieuwe tijdstip.",

            ("nl", "rejected.subject") => "Afspraakaanvraag - Hakim",
            ("nl", "rejected.title") => "Tijdslot niet bevestigd",
            ("nl", "rejected.textIntro") => "Bedankt voor je afspraakaanvraag voor {0}.",
            ("nl", "rejected.textBody") => "Ik kan dit tijdslot helaas niet bevestigen.",
            ("nl", "rejected.bodyHtml") => "Bedankt voor je afspraakaanvraag voor <strong>{0}</strong>.",
            ("nl", "rejected.cardTitle") => "Tijdslot vrijgegeven",
            ("nl", "rejected.cardText") => "Ik kan dit tijdslot helaas niet bevestigen.",

            ("nl", "rescheduleRejected.subject") => "Wijziging van tijdstip - Hakim",
            ("nl", "rescheduleRejected.title") => "Wijziging niet bevestigd",
            ("nl", "rescheduleRejected.textIntro") => "Ik kan de gevraagde wijziging van het tijdstip niet bevestigen.",
            ("nl", "rescheduleRejected.textBody") => "De oorspronkelijke afspraak blijft bevestigd voor {0}.",
            ("nl", "rescheduleRejected.bodyHtml") => "Ik kan de gevraagde wijziging van het tijdstip niet bevestigen. De oorspronkelijke afspraak blijft bevestigd voor <strong>{0}</strong>.",

            ("nl", "ownerCancelled.subject") => "Afspraak geannuleerd - Hakim",
            ("nl", "ownerCancelled.title") => "Afspraak geannuleerd",
            ("nl", "ownerCancelled.textIntro") => "De afspraak gepland voor {0} is geannuleerd.",
            ("nl", "ownerCancelled.bodyHtml") => "De afspraak gepland voor <strong>{0}</strong> is geannuleerd.",

            ("nl", "ownerRescheduled.subject") => "Nieuw tijdstip voor de afspraak - Hakim",
            ("nl", "ownerRescheduled.title") => "Afspraak verplaatst",
            ("nl", "ownerRescheduled.textIntro") => "De afspraak is verplaatst.",
            ("nl", "ownerRescheduled.bodyHtml") => "De afspraak is verplaatst naar <strong>{0}</strong>.",

            ("nl", "visitorCancelled.subject") => "Afspraakaanvraag geannuleerd - Hakim",
            ("nl", "visitorCancelled.title") => "Aanvraag geannuleerd",
            ("nl", "visitorCancelled.textIntro") => "Je afspraakaanvraag voor {0} is geannuleerd.",
            ("nl", "visitorCancelled.textBody") => "Het tijdslot is vrijgegeven.",
            ("nl", "visitorCancelled.bodyHtml") => "Je afspraakaanvraag voor <strong>{0}</strong> is geannuleerd.",
            ("nl", "visitorCancelled.cardText") => "Er is geen verdere actie nodig.",

            ("nl", "requestReceived.subject") => "Afspraakaanvraag ontvangen - Hakim",
            ("nl", "requestReceived.title") => "Aanvraag ontvangen",
            ("nl", "requestReceived.eyebrow") => "Booking request",
            ("nl", "requestReceived.textIntro") => "Ik heb je afspraakaanvraag goed ontvangen.",
            ("nl", "requestReceived.pendingLine") => "Het tijdslot is nog niet bevestigd. Ik valideer aanvragen handmatig en antwoord per e-mail.",
            ("nl", "requestReceived.cardTitle") => "Wacht op validatie",
            ("nl", "requestReceived.cardText") => "Het tijdslot is nog niet bevestigd. Ik valideer aanvragen handmatig en antwoord per e-mail.",

            ("nl", "rescheduleRequested.subject") => "Wijziging van tijdstip aangevraagd - Hakim",
            ("nl", "rescheduleRequested.title") => "Wijziging aangevraagd",
            ("nl", "rescheduleRequested.eyebrow") => "Reschedule request",
            ("nl", "rescheduleRequested.textIntro") => "Ik heb je aanvraag voor een wijziging naar {0} goed ontvangen.",
            ("nl", "rescheduleRequested.bodyHtml") => "Ik heb je aanvraag voor een wijziging naar <strong>{0}</strong> goed ontvangen.",
            ("nl", "rescheduleRequested.keepCurrent") => "De huidige afspraak blijft bevestigd voor {0} zolang het nieuwe tijdstip niet is aanvaard.",
            ("nl", "rescheduleRequested.pendingOnly") => "De aanvraag wacht op validatie voor het nieuwe tijdstip.",

            ("nl", "generic.eyebrow") => "Booking update",
            ("nl", "cancelled.cardTitle") => "Tijdslot vrijgegeven",
            ("nl", "reply.note") => "Als je iets wilt toevoegen, kun je rechtstreeks op deze e-mail antwoorden.",
            ("nl", "reply.offerAnother") => "Je kunt op deze e-mail antwoorden als je een ander moment wilt voorstellen.",
            ("nl", "button.google") => "Toevoegen aan Google Calendar",
            ("nl", "button.manageCancel") => "Beheren / annuleren",
            ("nl", "button.manageCancelRequest") => "Aanvraag beheren / annuleren",
            ("nl", "field.date") => "Datum",
            ("nl", "field.newDate") => "Nieuwe datum",
            ("nl", "field.type") => "Type",
            ("nl", "field.topic") => "Onderwerp",
            ("nl", "field.logistics") => "Praktische info",
            ("nl", "logistics.cardTitle") => "Praktische informatie",
            ("nl", "logistics.call.withPhone") => "Je kunt mij bereiken op {0} op het afgesproken tijdstip.",
            ("nl", "logistics.call.pending") => "Het telefoonnummer wordt per e-mail bevestigd.",
            ("nl", "logistics.inperson.withPhone") => "Afspraakadres: {0}. Indien nodig kun je mij op {1} bereiken op het afgesproken tijdstip.",
            ("nl", "logistics.inperson.addressOnly") => "Afspraakadres: {0}.",
            ("nl", "kind.video") => "Video",
            ("nl", "kind.call") => "Telefoongesprek",
            ("nl", "kind.inperson") => "In persoon",
            ("nl", "calendar.title") => "Afspraak met Hakim - {0}",
            ("nl", "calendar.detailsIntro") => "Afspraak bevestigd via iamhakim.com",
            ("nl", "calendar.location.video") => "Videolink nog te bevestigen",
            ("nl", "calendar.location.call") => "Telefoongesprek",
            ("nl", "calendar.location.inperson") => "Locatie nog te bevestigen",

            (_, "verification.subject") => "Verification code - Hakim",
            (_, "verification.title") => "Verification code",
            (_, "verification.eyebrow") => "Booking security",
            (_, "verification.heading") => "Confirm your email address",
            (_, "verification.codeLine") => "Here is your code to confirm your email address:",
            (_, "verification.expiryLine") => "It expires at {0}.",
            (_, "verification.bodyHtml") => "Use this code to finish your meeting request. It expires at <strong>{0}</strong>.",
            (_, "verification.ignore") => "If you did not start this request, you can safely ignore this email.",

            (_, "accepted.subject") => "Meeting confirmed - Hakim",
            (_, "accepted.title") => "Meeting confirmed",
            (_, "accepted.eyebrow") => "Booking confirmed",
            (_, "accepted.textIntro") => "Your meeting request is confirmed.",
            (_, "accepted.bodyHtml") => "Confirmed for <strong>{0}</strong>.",
            (_, "accepted.cardTitle") => "Meeting approved",
            (_, "accepted.cardText") => "The time slot is confirmed. You can add it to your calendar or keep this email as a reference.",

            (_, "rescheduleAccepted.subject") => "New time confirmed - Hakim",
            (_, "rescheduleAccepted.title") => "Time updated",
            (_, "rescheduleAccepted.eyebrow") => "Reschedule confirmed",
            (_, "rescheduleAccepted.textIntro") => "The time change is confirmed.",
            (_, "rescheduleAccepted.bodyHtml") => "The meeting is now confirmed for <strong>{0}</strong>.",
            (_, "rescheduleAccepted.cardTitle") => "New time approved",
            (_, "rescheduleAccepted.cardText") => "The previous time slot is replaced by this new one.",

            (_, "rejected.subject") => "Meeting request - Hakim",
            (_, "rejected.title") => "Time slot not confirmed",
            (_, "rejected.textIntro") => "Thank you for your meeting request for {0}.",
            (_, "rejected.textBody") => "Unfortunately, I cannot confirm this time slot.",
            (_, "rejected.bodyHtml") => "Thank you for your meeting request for <strong>{0}</strong>.",
            (_, "rejected.cardTitle") => "Time slot released",
            (_, "rejected.cardText") => "Unfortunately, I cannot confirm this time slot.",

            (_, "rescheduleRejected.subject") => "Time change - Hakim",
            (_, "rescheduleRejected.title") => "Change not confirmed",
            (_, "rescheduleRejected.textIntro") => "I cannot confirm the requested time change.",
            (_, "rescheduleRejected.textBody") => "The initial meeting remains confirmed for {0}.",
            (_, "rescheduleRejected.bodyHtml") => "I cannot confirm the requested time change. The initial meeting remains confirmed for <strong>{0}</strong>.",

            (_, "ownerCancelled.subject") => "Meeting cancelled - Hakim",
            (_, "ownerCancelled.title") => "Meeting cancelled",
            (_, "ownerCancelled.textIntro") => "The meeting planned for {0} has been cancelled.",
            (_, "ownerCancelled.bodyHtml") => "The meeting planned for <strong>{0}</strong> has been cancelled.",

            (_, "ownerRescheduled.subject") => "New meeting time - Hakim",
            (_, "ownerRescheduled.title") => "Meeting moved",
            (_, "ownerRescheduled.textIntro") => "The meeting has been moved.",
            (_, "ownerRescheduled.bodyHtml") => "The meeting has been moved to <strong>{0}</strong>.",

            (_, "visitorCancelled.subject") => "Meeting request cancelled - Hakim",
            (_, "visitorCancelled.title") => "Request cancelled",
            (_, "visitorCancelled.textIntro") => "Your meeting request for {0} has been cancelled.",
            (_, "visitorCancelled.textBody") => "The time slot has been released.",
            (_, "visitorCancelled.bodyHtml") => "Your meeting request for <strong>{0}</strong> has been cancelled.",
            (_, "visitorCancelled.cardText") => "No further action is needed.",

            (_, "requestReceived.subject") => "Meeting request received - Hakim",
            (_, "requestReceived.title") => "Request received",
            (_, "requestReceived.eyebrow") => "Booking request",
            (_, "requestReceived.textIntro") => "I have received your meeting request.",
            (_, "requestReceived.pendingLine") => "The time slot is not confirmed yet. I review requests manually and will reply by email.",
            (_, "requestReceived.cardTitle") => "Waiting for validation",
            (_, "requestReceived.cardText") => "The time slot is not confirmed yet. I review requests manually and will reply by email.",

            (_, "rescheduleRequested.subject") => "Time change requested - Hakim",
            (_, "rescheduleRequested.title") => "Change requested",
            (_, "rescheduleRequested.eyebrow") => "Reschedule request",
            (_, "rescheduleRequested.textIntro") => "I have received your request to move the meeting to {0}.",
            (_, "rescheduleRequested.bodyHtml") => "I have received your request to move the meeting to <strong>{0}</strong>.",
            (_, "rescheduleRequested.keepCurrent") => "The current meeting remains confirmed for {0} until the new time is accepted.",
            (_, "rescheduleRequested.pendingOnly") => "The request is waiting for validation on the new time slot.",

            (_, "generic.eyebrow") => "Booking update",
            (_, "cancelled.cardTitle") => "Time slot released",
            (_, "reply.note") => "If you need to add anything, you can reply directly to this email.",
            (_, "reply.offerAnother") => "You can reply to this email if you want to suggest another time.",
            (_, "button.google") => "Add to Google Calendar",
            (_, "button.manageCancel") => "Manage / cancel",
            (_, "button.manageCancelRequest") => "Manage / cancel request",
            (_, "field.date") => "Date",
            (_, "field.newDate") => "New date",
            (_, "field.type") => "Type",
            (_, "field.topic") => "Topic",
            (_, "field.logistics") => "Meeting details",
            (_, "logistics.cardTitle") => "Practical details",
            (_, "logistics.call.withPhone") => "You can contact me at {0} at the scheduled time.",
            (_, "logistics.call.pending") => "The phone number will be confirmed by email.",
            (_, "logistics.inperson.withPhone") => "Meeting address: {0}. If needed, you can contact me at {1} at the scheduled time.",
            (_, "logistics.inperson.addressOnly") => "Meeting address: {0}.",
            (_, "kind.video") => "Video call",
            (_, "kind.call") => "Phone call",
            (_, "kind.inperson") => "In person",
            (_, "calendar.title") => "Meeting with Hakim - {0}",
            (_, "calendar.detailsIntro") => "Meeting confirmed via iamhakim.com",
            (_, "calendar.location.video") => "Video link to be confirmed",
            (_, "calendar.location.call") => "Phone call",
            (_, "calendar.location.inperson") => "Location to be confirmed",

            _ => key
        };
    }

    private static string Layout(string title, string eyebrow, params string[] blocks)
    {
        string body = string.Join("", blocks.Select(RenderBlock));

        return
            "<!doctype html>" +
            "<html>" +
            "<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"></head>" +
            "<body style=\"margin:0;padding:0;background:#05070d;font-family:Inter,Segoe UI,Arial,sans-serif;color:#f7f7f8\">" +
            HiddenPreheader($"{title} - iamhakim.com") +
            "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#05070d;margin:0;padding:0\">" +
            "<tr><td align=\"center\" style=\"padding:32px 16px\">" +
            "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:660px;border-collapse:separate;border-spacing:0;background:#0b0f1a;border:1px solid #202637;border-radius:24px;overflow:hidden;box-shadow:0 28px 80px rgba(0,0,0,0.35)\">" +
            "<tr><td style=\"height:3px;background:linear-gradient(90deg,#f0a92b 0%,#ff5c7a 55%,#46e3d0 100%)\"></td></tr>" +
            "<tr><td style=\"padding:28px 30px 8px\">" +
            "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr>" +
            "<td style=\"vertical-align:top\">" +
            $"<div style=\"margin:0 0 12px;color:#46e3d0;font-size:11px;font-weight:800;letter-spacing:0.16em;text-transform:uppercase\">{H(eyebrow)}</div>" +
            $"<h1 style=\"margin:0;color:#f7f7f8;font-size:28px;line-height:1.16;font-weight:800;letter-spacing:-0.03em\">{H(title)}</h1>" +
            "</td>" +
            "<td align=\"right\" style=\"vertical-align:top;padding-left:16px\">" +
            "<span style=\"display:inline-block;padding:8px 11px;border-radius:999px;background:#121827;border:1px solid #273044;color:#9aa3b8;font-size:12px;font-weight:700\">iamhakim.com</span>" +
            "</td>" +
            "</tr></table>" +
            "</td></tr>" +
            $"<tr><td style=\"padding:20px 30px 30px\">{body}</td></tr>" +
            "<tr><td style=\"padding:20px 30px;border-top:1px solid #202637;background:#080c15\">" +
            "<p style=\"margin:0;color:#8d94a7;font-size:13px;line-height:1.5\"><strong style=\"color:#dfe4ef\">Hakim</strong><br>Full-stack developer · access systems<br><a href=\"https://iamhakim.com\" style=\"color:#46e3d0;text-decoration:none\">iamhakim.com</a></p>" +
            "</td></tr>" +
            "</table>" +
            "</td></tr>" +
            "</table>" +
            "</body></html>";
    }

    private static string RenderBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block)) return string.Empty;
        string trimmed = block.TrimStart();
        if (trimmed.StartsWith("<", StringComparison.Ordinal)) return block;
        return $"<p style=\"margin:0 0 16px;color:#cbd3e3;font-size:15px;line-height:1.65\">{block}</p>";
    }

    private static string HiddenPreheader(string value) =>
        $"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:transparent\">{H(value)}</div>";

    private static string CodeBlock(string code)
    {
        return
            "<div style=\"margin:22px 0;padding:22px;border-radius:18px;background:#060a12;border:1px solid #273044;text-align:center\">" +
            $"<div style=\"font-family:ui-monospace,SFMono-Regular,Consolas,monospace;color:#f7f7f8;font-size:34px;font-weight:800;letter-spacing:0.2em\">{H(code)}</div>" +
            "</div>";
    }

    private static string HighlightCard(string title, string text)
    {
        return
            "<div style=\"margin:18px 0;padding:16px 18px;border-radius:18px;background:rgba(70,227,208,0.07);border:1px solid rgba(70,227,208,0.24)\">" +
            $"<div style=\"margin:0 0 6px;color:#46e3d0;font-size:12px;font-weight:800;letter-spacing:0.12em;text-transform:uppercase\">{H(title)}</div>" +
            $"<div style=\"color:#dfe4ef;font-size:14px;line-height:1.55\">{H(text)}</div>" +
            "</div>";
    }

    private static string Note(string value) =>
        $"<p style=\"margin:18px 0 0;color:#8d94a7;font-size:13px;line-height:1.55\">{H(value)}</p>";

    private static string Button(string url, string label) =>
        $"<a href=\"{H(url)}\" style=\"display:inline-block;padding:14px 20px;border-radius:14px;background:linear-gradient(135deg,#f0a92b,#ff5c7a);color:#070a12;text-decoration:none;font-weight:800;font-size:14px;letter-spacing:0.01em\">{H(label)}</a>";

    private static string SecondaryButton(string url, string label) =>
        $"<a href=\"{H(url)}\" style=\"display:inline-block;padding:13px 18px;border-radius:14px;background:#101624;border:1px solid #273044;color:#dfe4ef;text-decoration:none;font-weight:800;font-size:14px;letter-spacing:0.01em\">{H(label)}</a>";

    private static string ButtonRow(params string[] buttons)
    {
        string cells = string.Join("", buttons.Select(button => $"<td style=\"padding:0 10px 10px 0\">{button}</td>"));
        return $"<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:24px 0 0\"><tr>{cells}</tr></table>";
    }

    private static string RequestSummaryCard(string name, string email, string when, string kind, string topic)
    {
        return
            "<div style=\"margin:20px 0 0;border:1px solid #273044;border-radius:20px;background:#090e18;overflow:hidden\">" +
            "<div style=\"padding:16px 18px;border-bottom:1px solid #202637;background:#0f1422\">" +
            "<div style=\"color:#8d94a7;font-size:12px;font-weight:800;letter-spacing:0.14em;text-transform:uppercase\">Demande entrante</div>" +
            $"<div style=\"margin-top:6px;color:#f7f7f8;font-size:18px;font-weight:800\">{H(name)}</div>" +
            $"<div style=\"margin-top:3px;color:#46e3d0;font-size:13px\">{H(email)}</div>" +
            "</div>" +
            DetailsTable(new (string Key, string Value)[]
            {
                ("Date", when),
                ("Type", kind),
                ("Sujet", topic)
            }) +
            "</div>";
    }

    private static string DetailsTable(IEnumerable<(string Key, string Value)> rows)
    {
        string htmlRows = string.Join("", rows.Select(row =>
            "<tr>" +
            $"<td style=\"width:140px;padding:13px 16px;color:#8d94a7;border-top:1px solid #202637;vertical-align:top;font-size:13px\">{H(row.Key)}</td>" +
            $"<td style=\"padding:13px 16px;color:#f7f7f8;border-top:1px solid #202637;vertical-align:top;font-size:14px;font-weight:700;line-height:1.45\">{H(row.Value)}</td>" +
            "</tr>"));

        return
            "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;margin:18px 0 0;background:#0b101b;border:1px solid #202637;border-radius:18px;overflow:hidden\">" +
            htmlRows +
            "</table>";
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);

    private static string U(string value) => Uri.EscapeDataString(value);
}

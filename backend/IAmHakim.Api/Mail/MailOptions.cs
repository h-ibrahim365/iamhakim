namespace IAmHakim.Api.Mail;

/// <summary>Bound from configuration section "Mail".</summary>
public sealed class MailOptions
{
    /// <summary>When false, booking e-mails are logged and skipped.</summary>
    public bool Enabled { get; set; }

    public string FromEmail { get; set; } = "booking@iamhakim.com";

    public string FromName { get; set; } = "Hakim";

    /// <summary>Where new booking requests are sent for manual review.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>Default reply-to used for visitor-facing messages.</summary>
    public string ReplyToEmail { get; set; } = "contact@iamhakim.com";

    /// <summary>Resend API key. Prefer environment variable Mail__ResendApiKey.</summary>
    public string ResendApiKey { get; set; } = string.Empty;

    public string ResendEndpoint { get; set; } = "https://api.resend.com/emails";
}

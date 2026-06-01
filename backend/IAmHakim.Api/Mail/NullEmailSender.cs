using Microsoft.Extensions.Options;

namespace IAmHakim.Api.Mail;

/// <summary>Development sender used when Mail:Enabled is false.</summary>
public sealed class NullEmailSender(
    ILogger<NullEmailSender> logger,
    IOptions<MailOptions> options) : IEmailSender
{
    private readonly MailOptions opt = options.Value;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "Mail disabled. Would send '{Subject}' from {From} to {To}.",
            message.Subject,
            opt.FromEmail,
            message.To);

        return Task.CompletedTask;
    }
}

namespace IAmHakim.Api.Mail;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

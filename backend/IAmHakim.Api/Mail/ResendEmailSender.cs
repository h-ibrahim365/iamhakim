using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace IAmHakim.Api.Mail;

/// <summary>Transaction e-mail sender backed by Resend.</summary>
public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<MailOptions> options) : IEmailSender
{
    private readonly MailOptions opt = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        EnsureConfigured();

        using HttpRequestMessage request = new(HttpMethod.Post, opt.ResendEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.ResendApiKey);

        ResendEmailRequest payload = new(
            From: FormatSender(opt.FromName, opt.FromEmail),
            To: [message.To],
            Subject: message.Subject,
            Text: message.TextBody,
            Html: message.HtmlBody,
            ReplyTo: string.IsNullOrWhiteSpace(message.ReplyTo) ? null : [message.ReplyTo]);

        request.Content = JsonContent.Create(payload);

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Resend failed with {(int)response.StatusCode}: {body}");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(opt.ResendApiKey))
        {
            throw new InvalidOperationException("Missing Mail:ResendApiKey configuration.");
        }

        if (string.IsNullOrWhiteSpace(opt.FromEmail))
        {
            throw new InvalidOperationException("Missing Mail:FromEmail configuration.");
        }
    }

    private static string FormatSender(string name, string email)
    {
        return string.IsNullOrWhiteSpace(name)
            ? email.Trim()
            : $"{name.Trim()} <{email.Trim()}>";
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("reply_to")] string[]? ReplyTo);
}

namespace IAmHakim.Api.Models;

/// <summary>
/// Short-lived proof that a visitor can receive mail at the address used in the
/// booking form. A verified token is consumed when the booking request is created.
/// </summary>
public sealed class EmailVerification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Email { get; set; } = string.Empty;

    /// <summary>Salted hash of the requester IP, used only for anti-abuse limits.</summary>
    public string IpHash { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public string? VerificationToken { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? VerifiedAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

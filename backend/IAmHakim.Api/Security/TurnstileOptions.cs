namespace IAmHakim.Api.Security;

public sealed class TurnstileOptions
{
    public bool Enabled { get; set; }

    /// <summary>Public site key used by the Angular widget.</summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>Private secret key used only by the API for Siteverify.</summary>
    public string SecretKey { get; set; } = string.Empty;

    public string VerifyEndpoint { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
}

public sealed record TurnstilePublicConfig(bool Enabled, string SiteKey);

public sealed record PublicConfigResponse(TurnstilePublicConfig Turnstile);

public sealed record TurnstileVerificationResult(bool Success, string? ErrorCode, string? ErrorMessage)
{
    public static TurnstileVerificationResult Ok() => new(true, null, null);

    public static TurnstileVerificationResult Failed(string code, string message) => new(false, code, message);
}

using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace IAmHakim.Api.Security;

/// <summary>
/// Server-side Cloudflare Turnstile verifier. The frontend token is only trusted
/// after Cloudflare's Siteverify endpoint confirms it.
/// </summary>
public sealed class TurnstileVerifier(
    HttpClient httpClient,
    IOptions<TurnstileOptions> options,
    ILogger<TurnstileVerifier> logger)
{
    private readonly TurnstileOptions opt = options.Value;

    public async Task<TurnstileVerificationResult> VerifyAsync(string? token, string? remoteIp, CancellationToken ct)
    {
        if (!opt.Enabled)
        {
            return TurnstileVerificationResult.Ok();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return TurnstileVerificationResult.Failed("turnstile_required", "Security check is required.");
        }

        if (string.IsNullOrWhiteSpace(opt.SecretKey))
        {
            logger.LogError("Cloudflare Turnstile is enabled but Security:Turnstile:SecretKey is missing.");
            return TurnstileVerificationResult.Failed("turnstile_misconfigured", "Security check is not configured correctly.");
        }

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["secret"] = opt.SecretKey,
            ["response"] = token,
            ["remoteip"] = remoteIp ?? string.Empty
        });

        try
        {
            using HttpResponseMessage response = await httpClient.PostAsync(opt.VerifyEndpoint, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Cloudflare Turnstile Siteverify failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return TurnstileVerificationResult.Failed("turnstile_unavailable", "Security check could not be validated.");
            }

            TurnstileSiteverifyResponse? payload = await response.Content.ReadFromJsonAsync<TurnstileSiteverifyResponse>(cancellationToken: ct);
            if (payload?.Success == true)
            {
                return TurnstileVerificationResult.Ok();
            }

            string errors = payload?.ErrorCodes is { Length: > 0 }
                ? string.Join(",", payload.ErrorCodes)
                : "unknown";

            logger.LogInformation("Cloudflare Turnstile rejected a token. Errors: {Errors}.", errors);
            return TurnstileVerificationResult.Failed("turnstile_invalid", "Security check failed. Please try again.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cloudflare Turnstile Siteverify call failed.");
            return TurnstileVerificationResult.Failed("turnstile_unavailable", "Security check could not be validated.");
        }
    }

    private sealed class TurnstileSiteverifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}

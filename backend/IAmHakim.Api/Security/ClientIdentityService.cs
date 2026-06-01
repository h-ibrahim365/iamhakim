using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace IAmHakim.Api.Security;

public sealed class ClientIdentityOptions
{
    /// <summary>
    /// Secret salt used before hashing visitor IP addresses. Set this via
    /// Security__ClientIdentity__IpHashSalt in production and keep it private.
    /// </summary>
    public string IpHashSalt { get; set; } = string.Empty;
}

public sealed record ClientIdentity(string? IpAddress, string IpHash);

/// <summary>
/// Extracts the client address once at the edge of the application and converts
/// it to a salted hash. The raw IP is never persisted by this service.
/// </summary>
public sealed class ClientIdentityService(IOptions<ClientIdentityOptions> options)
{
    private readonly ClientIdentityOptions opt = options.Value;

    public ClientIdentity Get(HttpContext httpContext)
    {
        string? ipAddress = ResolveClientIp(httpContext);
        return new ClientIdentity(ipAddress, HashIp(ipAddress));
    }

    public string GetLiveClientKey(HttpContext? httpContext, string connectionId)
    {
        if (httpContext is null)
        {
            return $"connection:{connectionId}";
        }

        ClientIdentity identity = Get(httpContext);
        return string.IsNullOrWhiteSpace(identity.IpAddress)
            ? $"connection:{connectionId}"
            : $"ip:{identity.IpHash}";
    }

    private string HashIp(string? ipAddress)
    {
        string normalized = NormalizeIp(ipAddress);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "unknown";
        }

        string salt = string.IsNullOrWhiteSpace(opt.IpHashSalt)
            ? "iamhakim-dev-change-me"
            : opt.IpHashSalt;

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{normalized}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeIp(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return string.Empty;
        }

        if (!IPAddress.TryParse(ipAddress.Trim(), out IPAddress? parsed))
        {
            return ipAddress.Trim().ToLowerInvariant();
        }

        if (parsed.IsIPv4MappedToIPv6)
        {
            parsed = parsed.MapToIPv4();
        }

        return parsed.ToString();
    }

    private static string? ResolveClientIp(HttpContext httpContext)
    {
        string? cfConnectingIp = FirstHeaderValue(httpContext, "CF-Connecting-IP");
        if (IsValidIp(cfConnectingIp)) return cfConnectingIp;

        string? xForwardedFor = FirstHeaderValue(httpContext, "X-Forwarded-For");
        if (!string.IsNullOrWhiteSpace(xForwardedFor))
        {
            foreach (string candidate in xForwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (IsValidIp(candidate)) return candidate;
            }
        }

        string? xRealIp = FirstHeaderValue(httpContext, "X-Real-IP");
        if (IsValidIp(xRealIp)) return xRealIp;

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? FirstHeaderValue(HttpContext httpContext, string name)
    {
        return httpContext.Request.Headers.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues values)
            ? values.FirstOrDefault()
            : null;
    }

    private static bool IsValidIp(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value.Trim(), out _);
    }
}

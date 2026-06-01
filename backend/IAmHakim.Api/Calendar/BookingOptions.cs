namespace IAmHakim.Api.Calendar;

/// <summary>Bound from configuration section "Booking".</summary>
public sealed class BookingOptions
{
    /// <summary>"mock" | "live". Mock needs no credentials and is the dev default.</summary>
    public string Mode { get; set; } = "mock";

    /// <summary>IANA timezone the availability is expressed in, e.g. "Europe/Brussels".</summary>
    public string TimeZone { get; set; } = "Europe/Brussels";

    /// <summary>Slot length in minutes.</summary>
    public int SlotMinutes { get; set; } = 30;

    /// <summary>Earliest bookable hour (local), inclusive. e.g. 18 = 18:00.</summary>
    public int WorkdayStartHour { get; set; } = 18;

    /// <summary>Latest bookable hour (local), exclusive end of last slot. e.g. 21 = last slot 20:30-21:00.</summary>
    public int WorkdayEndHour { get; set; } = 21;

    /// <summary>Days of week open for booking (0=Sunday..6=Saturday).</summary>
    public int[] OpenDays { get; set; } = [2, 3, 5, 6]; // Tue, Wed, Fri, Sat

    /// <summary>How far ahead visitors can book.</summary>
    public int HorizonDays { get; set; } = 90;

    /// <summary>Minimum notice before a slot (hours).</summary>
    public int MinNoticeHours { get; set; } = 12;

    /// <summary>How long a pending request blocks its slot before it is automatically released.</summary>
    public int PendingExpirationHours { get; set; } = 48;

    /// <summary>Base URL used to build backend-owned links, e.g. admin decision URLs.</summary>
    public string PublicBaseUrl { get; set; } = "https://iamhakim.com";

    /// <summary>Optional frontend URL used for visitor-facing links, e.g. "http://localhost:4200" in local dev.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    public BookingAntiSpamOptions AntiSpam { get; set; } = new();

    public BookingMeetingOptions Meeting { get; set; } = new();

    public GoogleOptions Google { get; set; } = new();

    public GraphOptions Graph { get; set; } = new();

    public IcsOptions Ics { get; set; } = new();
}

public sealed class BookingAntiSpamOptions
{
    /// <summary>Maximum pending requests allowed for the same e-mail address.</summary>
    public int MaxPendingPerEmail { get; set; } = 2;

    /// <summary>Maximum pending requests allowed for the same salted IP hash.</summary>
    public int MaxPendingPerIp { get; set; } = 3;

    /// <summary>Minimum delay between two requests from the same e-mail address.</summary>
    public int CooldownMinutesPerEmail { get; set; } = 60;

    /// <summary>Minimum delay between two requests from the same salted IP hash.</summary>
    public int CooldownMinutesPerIp { get; set; } = 10;

    /// <summary>Maximum requests accepted from the same e-mail address over a rolling 24h window.</summary>
    public int MaxRequestsPerEmailPerDay { get; set; } = 5;

    /// <summary>Maximum requests accepted from the same salted IP hash over a rolling 24h window.</summary>
    public int MaxRequestsPerIpPerDay { get; set; } = 12;

    /// <summary>Maximum verification codes for the same salted IP hash over a rolling 1h window.</summary>
    public int MaxVerificationCodesPerIpPerHour { get; set; } = 8;

    /// <summary>Maximum verification codes for the same salted IP hash over a rolling 24h window.</summary>
    public int MaxVerificationCodesPerIpPerDay { get; set; } = 20;
}

public sealed class BookingMeetingOptions
{
    /// <summary>Optional phone number shown for phone-call logistics.</summary>
    public string OwnerPhoneNumber { get; set; } = string.Empty;
}

public sealed class GoogleOptions
{
    public bool Enabled { get; set; }

    public bool IsPrimary { get; set; } = true;

    public string CalendarId { get; set; } = "primary";

    public string OwnerEmail { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;
}


public sealed class IcsOptions
{
    public bool Enabled { get; set; }

    public string[] Urls { get; set; } = [];

    public int CacheMinutes { get; set; } = 15;

    /// <summary>When true, even events marked TRANSPARENT / available block booking slots.</summary>
    public bool BlockTransparentEvents { get; set; } = true;
}

public sealed class GraphOptions
{
    public bool Enabled { get; set; }
    public bool IsPrimary { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string OwnerUpn { get; set; } = string.Empty;
}

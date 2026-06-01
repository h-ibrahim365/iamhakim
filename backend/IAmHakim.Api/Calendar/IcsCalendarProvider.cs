using System.Globalization;
using IAmHakim.Api.Models;
using Microsoft.Extensions.Options;

namespace IAmHakim.Api.Calendar;

/// <summary>
/// Read-only calendar provider that imports busy intervals from public iCalendar (.ics) feeds.
/// This is useful for external subscribed calendars such as school/university timetables that
/// may be displayed as "available" in Google Calendar but should still block booking slots.
/// </summary>
public sealed class IcsCalendarProvider : ICalendarProvider
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly BookingOptions bookingOptions;
    private readonly IcsOptions options;
    private readonly ILogger<IcsCalendarProvider> logger;
    private readonly SemaphoreSlim cacheLock = new(1, 1);

    private DateTimeOffset cacheExpiresAtUtc;
    private IReadOnlyList<BusyInterval> cachedBusyIntervals = [];

    public IcsCalendarProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<BookingOptions> bookingOptions,
        ILogger<IcsCalendarProvider> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.bookingOptions = bookingOptions.Value;
        options = this.bookingOptions.Ics;
        this.logger = logger;
    }

    public string Name => "ics";

    public bool IsPrimary => false;

    public async Task<IReadOnlyList<BusyInterval>> GetBusyAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        if (!options.Enabled || options.Urls.Length == 0)
        {
            return [];
        }

        IReadOnlyList<BusyInterval> busy = await GetCachedBusyIntervalsAsync(ct);

        return busy
            .Where(interval => interval.StartUtc < toUtc && interval.EndUtc > fromUtc)
            .ToList();
    }

    public Task<string> CreateEventAsync(Booking booking, CancellationToken ct)
    {
        throw new NotSupportedException("ICS calendars are read-only and cannot create booking events.");
    }

    public Task UpdateEventAsync(Booking booking, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task DeleteEventAsync(string calendarEventId, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<BusyInterval>> GetCachedBusyIntervalsAsync(CancellationToken ct)
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

        if (cachedBusyIntervals.Count > 0 && cacheExpiresAtUtc > nowUtc)
        {
            return cachedBusyIntervals;
        }

        await cacheLock.WaitAsync(ct);
        try
        {
            nowUtc = DateTimeOffset.UtcNow;
            if (cachedBusyIntervals.Count > 0 && cacheExpiresAtUtc > nowUtc)
            {
                return cachedBusyIntervals;
            }

            List<BusyInterval> merged = [];
            foreach (string rawUrl in options.Urls.Where(url => !string.IsNullOrWhiteSpace(url)))
            {
                try
                {
                    merged.AddRange(await ReadFeedAsync(rawUrl.Trim(), ct));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to read ICS calendar feed {FeedUrl}.", rawUrl);
                }
            }

            cachedBusyIntervals = merged
                .Where(interval => interval.EndUtc > interval.StartUtc)
                .OrderBy(interval => interval.StartUtc)
                .ToList();

            int cacheMinutes = Math.Clamp(options.CacheMinutes, 1, 120);
            cacheExpiresAtUtc = nowUtc.AddMinutes(cacheMinutes);

            return cachedBusyIntervals;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private async Task<List<BusyInterval>> ReadFeedAsync(string feedUrl, CancellationToken ct)
    {
        HttpClient client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("iamhakim.com-booking/1.0 (+https://iamhakim.com)");

        string ics = await client.GetStringAsync(feedUrl, ct);
        return ParseIcs(ics);
    }

    private List<BusyInterval> ParseIcs(string ics)
    {
        List<string> lines = UnfoldLines(ics);
        List<BusyInterval> busy = [];
        Dictionary<string, string> current = new(StringComparer.OrdinalIgnoreCase);
        bool inEvent = false;

        foreach (string line in lines)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current.Clear();
                inEvent = true;
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (TryBuildBusyInterval(current, out BusyInterval interval))
                {
                    busy.Add(interval);
                }

                inEvent = false;
                current.Clear();
                continue;
            }

            if (!inEvent)
            {
                continue;
            }

            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                continue;
            }

            string key = line[..colonIndex];
            string value = line[(colonIndex + 1)..];
            string propertyName = key.Split(';', 2)[0];

            current[propertyName] = value;
            current[$"{propertyName}__PARAMS"] = key;
        }

        return busy;
    }

    private bool TryBuildBusyInterval(Dictionary<string, string> evt, out BusyInterval interval)
    {
        interval = new BusyInterval(DateTimeOffset.MinValue, DateTimeOffset.MinValue);

        if (evt.TryGetValue("STATUS", out string? status) &&
            status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!options.BlockTransparentEvents &&
            evt.TryGetValue("TRANSP", out string? transparency) &&
            transparency.Equals("TRANSPARENT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!evt.TryGetValue("DTSTART", out string? startValue))
        {
            return false;
        }

        string startParams = evt.GetValueOrDefault("DTSTART__PARAMS", "DTSTART");
        string endParams = evt.GetValueOrDefault("DTEND__PARAMS", "DTEND");

        if (!TryParseIcsDate(startValue, startParams, out DateTimeOffset startUtc, out bool isAllDay))
        {
            return false;
        }

        DateTimeOffset endUtc;
        if (evt.TryGetValue("DTEND", out string? endValue) &&
            TryParseIcsDate(endValue, endParams, out DateTimeOffset parsedEndUtc, out _))
        {
            endUtc = parsedEndUtc;
        }
        else
        {
            endUtc = isAllDay ? startUtc.AddDays(1) : startUtc.AddMinutes(bookingOptions.SlotMinutes);
        }

        interval = new BusyInterval(startUtc, endUtc);
        return endUtc > startUtc;
    }

    private bool TryParseIcsDate(
        string value,
        string propertyWithParams,
        out DateTimeOffset utc,
        out bool isAllDay)
    {
        utc = default;
        isAllDay = false;

        value = value.Trim();
        string? tzid = ExtractParameter(propertyWithParams, "TZID");

        if (value.Length == 8 && DateTime.TryParseExact(
            value,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime dateOnly))
        {
            isAllDay = true;
            TimeZoneInfo timezone = ResolveTimeZone(tzid ?? bookingOptions.TimeZone);
            utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dateOnly, DateTimeKind.Unspecified), timezone));
            return true;
        }

        if (DateTime.TryParseExact(
            value,
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime zulu))
        {
            utc = new DateTimeOffset(DateTime.SpecifyKind(zulu, DateTimeKind.Utc));
            return true;
        }

        string[] localFormats = ["yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm"];
        if (DateTime.TryParseExact(
            value,
            localFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime local))
        {
            TimeZoneInfo timezone = ResolveTimeZone(tzid ?? bookingOptions.TimeZone);
            utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timezone));
            return true;
        }

        return false;
    }

    private static List<string> UnfoldLines(string ics)
    {
        List<string> lines = [];
        using StringReader reader = new(ics.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && lines.Count > 0)
            {
                lines[^1] += line[1..];
            }
            else
            {
                lines.Add(line.TrimEnd());
            }
        }

        return lines;
    }

    private static string? ExtractParameter(string propertyWithParams, string parameterName)
    {
        string[] parts = propertyWithParams.Split(';');
        foreach (string part in parts.Skip(1))
        {
            string[] pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0].Equals(parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return pair[1].Trim('"');
            }
        }

        return null;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}

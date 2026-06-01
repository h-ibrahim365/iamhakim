using IAmHakim.Api.Data;
using IAmHakim.Api.Mail;
using IAmHakim.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace IAmHakim.Api.Calendar;

public sealed class BookingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IEnumerable<ICalendarProvider> providers,
    BookingEmailService emails,
    IOptions<BookingOptions> options,
    ILogger<BookingService> logger)
{
    private readonly BookingOptions opt = options.Value;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    private TimeZoneInfo Tz => ResolveTimeZone(opt.TimeZone);

    // -- availability ---------------------------------------------------

    public async Task<AvailabilityResponse> GetAvailabilityAsync(CancellationToken ct)
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        DateTimeOffset horizonUtc = nowUtc.AddDays(opt.HorizonDays);

        await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
        await ExpirePendingAsync(db, nowUtc, ct);
        await db.SaveChangesAsync(ct);

        List<BusyInterval> busy = await GetMergedBusyAsync(nowUtc, horizonUtc, ct);
        HashSet<DateTimeOffset> bookedStarts = await GetBookedStartsAsync(nowUtc, horizonUtc, nowUtc, ct);

        TimeZoneInfo tz = Tz;
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, tz).DateTime);
        DateOnly last = today.AddDays(opt.HorizonDays);
        DateTimeOffset earliest = nowUtc.AddHours(opt.MinNoticeHours);

        List<AvailabilityDay> days = [];
        for (DateOnly date = today; date <= last; date = date.AddDays(1))
        {
            if (!opt.OpenDays.Contains((int)date.DayOfWeek)) continue;

            List<AvailabilitySlot> slots = [];
            for (int hour = opt.WorkdayStartHour; hour < opt.WorkdayEndHour; hour++)
            {
                foreach (int minute in MinutesInHour())
                {
                    DateTimeOffset startUtc = LocalToUtc(date, hour, minute, tz);
                    DateTimeOffset endUtc = startUtc.AddMinutes(opt.SlotMinutes);
                    if (endUtc > LocalToUtc(date, opt.WorkdayEndHour, 0, tz)) continue;

                    bool tooSoon = startUtc < earliest;
                    bool overlapsBusy = busy.Any(b => b.StartUtc < endUtc && b.EndUtc > startUtc);
                    bool alreadyBooked = bookedStarts.Contains(startUtc);
                    bool available = !tooSoon && !overlapsBusy && !alreadyBooked;

                    slots.Add(new AvailabilitySlot(SlotId(startUtc), startUtc, endUtc, available));
                }
            }

            if (slots.Count > 0) days.Add(new AvailabilityDay(date, slots));
        }

        return new AvailabilityResponse(today, last, opt.TimeZone, days);
    }

    private IEnumerable<int> MinutesInHour()
    {
        for (int m = 0; m < 60; m += opt.SlotMinutes) yield return m;
    }

    // -- e-mail verification --------------------------------------------

    public async Task<EmailVerificationResponse> RequestEmailVerificationAsync(EmailVerificationRequest request, string ipHash, CancellationToken ct)
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        string email = NormalizeEmail(request.Email);
        string safeIpHash = NormalizeIpHash(ipHash);

        await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);

        DateTimeOffset resendCutoff = nowUtc.AddMinutes(-1);
        bool recentlySent = await db.EmailVerifications.AnyAsync(v =>
            v.Email == email &&
            v.CreatedAtUtc >= resendCutoff &&
            v.ConsumedAtUtc == null, ct);

        if (recentlySent)
        {
            throw new InvalidOperationException("A verification code was just sent. Please wait a minute before requesting another one.");
        }

        bool recentlySentFromIp = await db.EmailVerifications.AnyAsync(v =>
            v.IpHash == safeIpHash &&
            v.CreatedAtUtc >= resendCutoff &&
            v.ConsumedAtUtc == null, ct);

        if (recentlySentFromIp)
        {
            throw new InvalidOperationException("A verification code was just sent from this network. Please wait a minute before requesting another one.");
        }

        DateTimeOffset hourlyCutoff = nowUtc.AddHours(-1);
        int recentCodes = await db.EmailVerifications.CountAsync(v =>
            v.Email == email &&
            v.CreatedAtUtc >= hourlyCutoff, ct);

        if (recentCodes >= 3)
        {
            throw new InvalidOperationException("Too many verification codes were requested for this email address. Please try again later.");
        }

        if (opt.AntiSpam.MaxVerificationCodesPerIpPerHour > 0)
        {
            int recentCodesFromIp = await db.EmailVerifications.CountAsync(v =>
                v.IpHash == safeIpHash &&
                v.CreatedAtUtc >= hourlyCutoff, ct);

            if (recentCodesFromIp >= opt.AntiSpam.MaxVerificationCodesPerIpPerHour)
            {
                throw new InvalidOperationException("Too many verification codes were requested from this network. Please try again later.");
            }
        }

        if (opt.AntiSpam.MaxVerificationCodesPerIpPerDay > 0)
        {
            DateTimeOffset dayCutoff = nowUtc.AddDays(-1);
            int dailyCodesFromIp = await db.EmailVerifications.CountAsync(v =>
                v.IpHash == safeIpHash &&
                v.CreatedAtUtc >= dayCutoff, ct);

            if (dailyCodesFromIp >= opt.AntiSpam.MaxVerificationCodesPerIpPerDay)
            {
                throw new InvalidOperationException("Too many verification codes were requested from this network today. Please try again later.");
            }
        }

        string code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        EmailVerification verification = new()
        {
            Email = email,
            IpHash = safeIpHash,
            CodeHash = HashEmailCode(email, code),
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddMinutes(15)
        };

        db.EmailVerifications.Add(verification);
        await db.SaveChangesAsync(ct);

        await emails.SendEmailVerificationCodeAsync(email, code, verification.ExpiresAtUtc, request.Language, ct);

        return new EmailVerificationResponse(verification.Id, verification.ExpiresAtUtc);
    }

    public async Task<EmailVerificationConfirmResponse> ConfirmEmailVerificationAsync(EmailVerificationConfirmRequest request, CancellationToken ct)
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        string email = NormalizeEmail(request.Email);
        string code = NormalizeVerificationCode(request.Code);

        await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
        EmailVerification verification = await db.EmailVerifications.FirstOrDefaultAsync(v =>
            v.Id == request.VerificationId &&
            v.Email == email &&
            v.ConsumedAtUtc == null, ct)
            ?? throw new InvalidOperationException("Unknown verification request.");

        if (verification.ExpiresAtUtc <= nowUtc)
        {
            throw new InvalidOperationException("This verification code has expired. Please request a new one.");
        }

        if (verification.Attempts >= 5)
        {
            throw new InvalidOperationException("Too many invalid attempts. Please request a new verification code.");
        }

        verification.Attempts++;

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(verification.CodeHash),
            Encoding.UTF8.GetBytes(HashEmailCode(email, code))))
        {
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Invalid verification code.");
        }

        verification.VerifiedAtUtc = nowUtc;
        verification.VerificationToken = NewToken();
        await db.SaveChangesAsync(ct);

        return new EmailVerificationConfirmResponse(email, verification.VerificationToken);
    }

    // -- create request -------------------------------------------------

    public async Task<BookingResponse> CreateBookingAsync(BookingRequest request, string ipHash, CancellationToken ct)
    {
        if (!TryParseSlot(request.SlotId, out DateTimeOffset startUtc))
            throw new InvalidOperationException("Invalid slot.");

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        DateTimeOffset endUtc = startUtc.AddMinutes(opt.SlotMinutes);
        ValidatePublicSlot(startUtc, endUtc, nowUtc);

        string name = NormalizeName(request.Name);
        string email = NormalizeEmail(request.Email);
        string safeIpHash = NormalizeIpHash(ipHash);
        string message = NormalizeMessage(request.Message);
        string? meetingLocation = NormalizeMeetingLocation(request.Kind, request.MeetingLocation);
        string language = BookingEmailService.NormalizeLanguage(request.Language);

        Booking booking;

        await writeLock.WaitAsync(ct);
        try
        {
            await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
            await ExpirePendingAsync(db, nowUtc, ct);
            await db.SaveChangesAsync(ct);

            await EnsureAntiSpamAsync(db, email, safeIpHash, nowUtc, ct);
            await EnsureVerifiedEmailTokenAsync(db, email, request.EmailVerificationToken, nowUtc, ct);
            await EnsureSlotStillFreeAsync(db, startUtc, endUtc, nowUtc, null, ct);

            booking = new Booking
            {
                ManageToken = NewToken(),
                DecisionToken = NewToken(),
                StartUtc = startUtc,
                EndUtc = endUtc,
                Kind = request.Kind,
                Name = name,
                Email = email,
                IpHash = safeIpHash,
                Message = message,
                MeetingLocation = meetingLocation,
                Language = language,
                Status = BookingStatuses.Pending,
                ExpiresAtUtc = nowUtc.AddHours(opt.PendingExpirationHours),
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }

        await TrySendAsync(
            () => emails.SendRequestCreatedAsync(
                booking,
                BuildManageUrl(booking.ManageToken),
                BuildAdminDecisionUrl(booking.DecisionToken),
                ct),
            booking.Id,
            "request-created");

        return ToResponse(booking);
    }

    // -- owner/admin flow ----------------------------------------------

    public async Task<BookingDecisionView?> GetDecisionByTokenAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        await ExpirePendingAsync(db, nowUtc, ct);
        await db.SaveChangesAsync(ct);

        Booking? booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.DecisionToken == token, ct);
        return booking is null ? null : ToDecisionView(booking);
    }

    public async Task<BookingResponse> AcceptAsync(string decisionToken, CancellationToken ct)
    {
        Booking booking;
        bool acceptedReschedule = false;

        await writeLock.WaitAsync(ct);
        try
        {
            await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            await ExpirePendingAsync(db, nowUtc, ct);
            await db.SaveChangesAsync(ct);

            booking = await db.Bookings.FirstOrDefaultAsync(x => x.DecisionToken == decisionToken, ct)
                ?? throw new InvalidOperationException("Unknown booking request.");

            if (booking.Status == BookingStatuses.RescheduleRequested)
            {
                if (booking.RequestedStartUtc is null || booking.RequestedEndUtc is null)
                {
                    throw new InvalidOperationException("Invalid reschedule request.");
                }

                await EnsureSlotStillFreeAsync(db, booking.RequestedStartUtc.Value, booking.RequestedEndUtc.Value, nowUtc, booking.Id, ct);

                booking.StartUtc = booking.RequestedStartUtc.Value;
                booking.EndUtc = booking.RequestedEndUtc.Value;
                booking.RequestedStartUtc = null;
                booking.RequestedEndUtc = null;

                ICalendarProvider primary = PrimaryProvider();
                booking.Provider = primary.Name;
                if (string.IsNullOrWhiteSpace(booking.CalendarEventId))
                {
                    booking.CalendarEventId = await primary.CreateEventAsync(booking, ct);
                }
                else
                {
                    await primary.UpdateEventAsync(booking, ct);
                }

                booking.Status = BookingStatuses.Accepted;
                booking.DecidedAtUtc = nowUtc;
                booking.ExpiresAtUtc = null;
                booking.UpdatedAtUtc = nowUtc;
                acceptedReschedule = true;

                await db.SaveChangesAsync(ct);
            }
            else if (booking.Status == BookingStatuses.Pending)
            {
                if (booking.ExpiresAtUtc is not null && booking.ExpiresAtUtc <= nowUtc)
                {
                    booking.Status = BookingStatuses.Expired;
                    booking.UpdatedAtUtc = nowUtc;
                    await db.SaveChangesAsync(ct);
                    throw new InvalidOperationException("This request has expired.");
                }

                await EnsureSlotStillFreeAsync(db, booking.StartUtc, booking.EndUtc, nowUtc, booking.Id, ct);

                ICalendarProvider primary = PrimaryProvider();
                booking.Provider = primary.Name;
                booking.CalendarEventId = await primary.CreateEventAsync(booking, ct);
                booking.Status = BookingStatuses.Accepted;
                booking.DecidedAtUtc = nowUtc;
                booking.ExpiresAtUtc = null;
                booking.UpdatedAtUtc = nowUtc;

                await db.SaveChangesAsync(ct);
            }
            else
            {
                throw new InvalidOperationException($"This request is already {booking.Status}.");
            }
        }
        finally
        {
            writeLock.Release();
        }

        await TrySendAsync(
            () => acceptedReschedule
                ? emails.SendRescheduleAcceptedAsync(booking, BuildManageUrl(booking.ManageToken), ct)
                : emails.SendAcceptedAsync(booking, BuildManageUrl(booking.ManageToken), ct),
            booking.Id,
            acceptedReschedule ? "reschedule-accepted" : "accepted");

        return ToResponse(booking);
    }

    public async Task<BookingResponse> RejectAsync(string decisionToken, CancellationToken ct)
    {
        Booking booking;
        bool rejectedReschedule = false;

        await writeLock.WaitAsync(ct);
        try
        {
            await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            await ExpirePendingAsync(db, nowUtc, ct);
            await db.SaveChangesAsync(ct);

            booking = await db.Bookings.FirstOrDefaultAsync(x => x.DecisionToken == decisionToken, ct)
                ?? throw new InvalidOperationException("Unknown booking request.");

            if (booking.Status == BookingStatuses.RescheduleRequested)
            {
                booking.RequestedStartUtc = null;
                booking.RequestedEndUtc = null;
                booking.Status = BookingStatuses.Accepted;
                booking.DecidedAtUtc = nowUtc;
                booking.ExpiresAtUtc = null;
                booking.UpdatedAtUtc = nowUtc;
                rejectedReschedule = true;
            }
            else if (booking.Status == BookingStatuses.Pending)
            {
                booking.Status = BookingStatuses.Rejected;
                booking.DecidedAtUtc = nowUtc;
                booking.UpdatedAtUtc = nowUtc;
            }
            else
            {
                throw new InvalidOperationException($"This request is already {booking.Status}.");
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }

        await TrySendAsync(
            () => rejectedReschedule
                ? emails.SendRescheduleRejectedAsync(booking, ct)
                : emails.SendRejectedAsync(booking, ct),
            booking.Id,
            rejectedReschedule ? "reschedule-rejected" : "rejected");

        return ToResponse(booking);
    }

    public async Task<BookingResponse> AdminCancelAsync(string decisionToken, CancellationToken ct)
    {
        Booking booking;

        await writeLock.WaitAsync(ct);
        try
        {
            await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            booking = await db.Bookings.FirstOrDefaultAsync(x => x.DecisionToken == decisionToken, ct)
                ?? throw new InvalidOperationException("Unknown booking request.");

            if (!CanOwnerCancel(booking.Status))
            {
                throw new InvalidOperationException($"This request is already {booking.Status}.");
            }

            ICalendarProvider primary = PrimaryProvider();
            if (booking.CalendarEventId is { } id) await primary.DeleteEventAsync(id, ct);

            booking.CalendarEventId = null;
            booking.RequestedStartUtc = null;
            booking.RequestedEndUtc = null;
            booking.Status = BookingStatuses.Cancelled;
            booking.UpdatedAtUtc = nowUtc;
            booking.DecidedAtUtc = nowUtc;
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }

        await TrySendAsync(
            () => emails.SendCancelledByOwnerAsync(booking, ct),
            booking.Id,
            "owner-cancelled");

        return ToResponse(booking);
    }

    public async Task<BookingResponse> AdminRescheduleAsync(string decisionToken, string newSlotId, CancellationToken ct)
    {
        if (!TryParseSlot(newSlotId, out DateTimeOffset newStart))
            throw new InvalidOperationException("Invalid new slot.");

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        DateTimeOffset newEnd = newStart.AddMinutes(opt.SlotMinutes);
        ValidatePublicSlot(newStart, newEnd, nowUtc);

        Booking booking;

        await writeLock.WaitAsync(ct);
        try
        {
            await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
            booking = await db.Bookings.FirstOrDefaultAsync(x => x.DecisionToken == decisionToken, ct)
                ?? throw new InvalidOperationException("Unknown booking request.");

            if (!CanOwnerReschedule(booking.Status))
            {
                throw new InvalidOperationException($"This request is already {booking.Status}.");
            }

            await EnsureSlotStillFreeAsync(db, newStart, newEnd, nowUtc, booking.Id, ct);

            booking.StartUtc = newStart;
            booking.EndUtc = newEnd;
            booking.RequestedStartUtc = null;
            booking.RequestedEndUtc = null;
            booking.DecidedAtUtc = nowUtc;
            booking.ExpiresAtUtc = null;
            booking.UpdatedAtUtc = nowUtc;

            if (booking.Status == BookingStatuses.Pending)
            {
                booking.Status = BookingStatuses.Pending;
            }
            else
            {
                ICalendarProvider primary = PrimaryProvider();
                booking.Provider = primary.Name;
                if (string.IsNullOrWhiteSpace(booking.CalendarEventId))
                {
                    booking.CalendarEventId = await primary.CreateEventAsync(booking, ct);
                }
                else
                {
                    await primary.UpdateEventAsync(booking, ct);
                }
                booking.Status = BookingStatuses.Accepted;
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }

        await TrySendAsync(
            () => emails.SendRescheduledByOwnerAsync(booking, BuildManageUrl(booking.ManageToken), ct),
            booking.Id,
            "owner-rescheduled");

        return ToResponse(booking);
    }

    // -- manage (anonymous, token-based) --------------------------------

    public async Task<BookingView?> GetByTokenAsync(string token, CancellationToken ct)
    {
        await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
        Booking? b = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.ManageToken == token, ct);
        return b is null ? null : ToView(b);
    }

    public async Task<BookingResponse> ManageAsync(ManageBookingRequest request, CancellationToken ct)
    {
        Booking booking;
        bool cancelled = false;
        bool rescheduledPending = false;
        bool rescheduleRequested = false;

        await writeLock.WaitAsync(ct);
        try
        {
            await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            await ExpirePendingAsync(db, nowUtc, ct);
            await db.SaveChangesAsync(ct);

            booking = await db.Bookings.FirstOrDefaultAsync(x => x.ManageToken == request.ManageToken, ct)
                ?? throw new InvalidOperationException("Unknown booking.");

            ICalendarProvider primary = PrimaryProvider();

            if (request.Action == "cancel")
            {
                if (!CanVisitorCancel(booking.Status))
                {
                    throw new InvalidOperationException($"This request is already {booking.Status}.");
                }

                if (booking.CalendarEventId is { } id) await primary.DeleteEventAsync(id, ct);
                booking.CalendarEventId = null;
                booking.RequestedStartUtc = null;
                booking.RequestedEndUtc = null;
                booking.Status = BookingStatuses.Cancelled;
                booking.UpdatedAtUtc = nowUtc;
                booking.DecidedAtUtc = nowUtc;
                cancelled = true;
            }
            else if (request.Action == "reschedule")
            {
                if (request.NewSlotId is null || !TryParseSlot(request.NewSlotId, out DateTimeOffset newStart))
                    throw new InvalidOperationException("Invalid new slot.");

                DateTimeOffset newEnd = newStart.AddMinutes(opt.SlotMinutes);
                ValidatePublicSlot(newStart, newEnd, nowUtc);
                await EnsureSlotStillFreeAsync(db, newStart, newEnd, nowUtc, booking.Id, ct);

                if (booking.Status == BookingStatuses.Pending)
                {
                    booking.StartUtc = newStart;
                    booking.EndUtc = newEnd;
                    booking.DecisionToken = NewToken();
                    booking.ExpiresAtUtc = nowUtc.AddHours(opt.PendingExpirationHours);
                    booking.UpdatedAtUtc = nowUtc;
                    rescheduledPending = true;
                }
                else if (booking.Status is BookingStatuses.Accepted or BookingStatuses.Confirmed or BookingStatuses.Rescheduled or BookingStatuses.RescheduleRequested)
                {
                    booking.RequestedStartUtc = newStart;
                    booking.RequestedEndUtc = newEnd;
                    booking.DecisionToken = NewToken();
                    booking.Status = BookingStatuses.RescheduleRequested;
                    booking.ExpiresAtUtc = nowUtc.AddHours(opt.PendingExpirationHours);
                    booking.UpdatedAtUtc = nowUtc;
                    rescheduleRequested = true;
                }
                else
                {
                    throw new InvalidOperationException($"This request is already {booking.Status}.");
                }
            }
            else
            {
                throw new InvalidOperationException("Unknown action.");
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }

        if (cancelled)
        {
            await TrySendAsync(
                () => emails.SendCancelledByVisitorAsync(booking, ct),
                booking.Id,
                "cancelled");
        }

        if (rescheduledPending || rescheduleRequested)
        {
            await TrySendAsync(
                () => emails.SendRescheduleRequestedAsync(
                    booking,
                    BuildManageUrl(booking.ManageToken),
                    BuildAdminDecisionUrl(booking.DecisionToken),
                    rescheduleRequested,
                    ct),
                booking.Id,
                rescheduleRequested ? "reschedule-requested" : "pending-rescheduled");
        }

        return ToResponse(booking);
    }

    // -- helpers --------------------------------------------------------

    public string BuildManageUrl(string manageToken) =>
        $"{VisitorBaseUrl()}/book/manage?token={manageToken}";

    public string BuildAdminDecisionUrl(string? decisionToken) =>
        $"{ApiBaseUrl()}/api/bookings/admin?token={decisionToken}";

    public string BuildFrontendRouteUrl(string route, string? queryString = null)
    {
        string normalizedRoute = route.StartsWith('/') ? route : $"/{route}";
        string normalizedQuery = string.IsNullOrWhiteSpace(queryString)
            ? string.Empty
            : queryString.StartsWith('?') ? queryString : $"?{queryString}";

        return $"{VisitorBaseUrl()}{normalizedRoute}{normalizedQuery}";
    }

    private string VisitorBaseUrl()
    {
        string baseUrl = string.IsNullOrWhiteSpace(opt.FrontendBaseUrl)
            ? opt.PublicBaseUrl
            : opt.FrontendBaseUrl;

        return baseUrl.TrimEnd('/');
    }

    private string ApiBaseUrl() => opt.PublicBaseUrl.TrimEnd('/');

    private async Task TrySendAsync(Func<Task> send, string bookingId, string eventName)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Booking mail step {EventName} failed for booking {BookingId}.", eventName, bookingId);
        }
    }

    private async Task<List<BusyInterval>> GetMergedBusyAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        List<BusyInterval> merged = [];

        foreach (ICalendarProvider provider in providers)
        {
            merged.AddRange(await provider.GetBusyAsync(fromUtc, toUtc, ct));
        }

        return merged;
    }

    private async Task<HashSet<DateTimeOffset>> GetBookedStartsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        await using AppDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
        List<Booking> bookings = await db.Bookings
            .Where(b => b.StartUtc >= fromUtc && b.StartUtc < toUtc ||
                (b.RequestedStartUtc != null && b.RequestedStartUtc >= fromUtc && b.RequestedStartUtc < toUtc))
            .Where(b =>
                b.Status == BookingStatuses.Accepted ||
                b.Status == BookingStatuses.Confirmed ||
                b.Status == BookingStatuses.Rescheduled ||
                b.Status == BookingStatuses.RescheduleRequested ||
                (b.Status == BookingStatuses.Pending && (b.ExpiresAtUtc == null || b.ExpiresAtUtc > nowUtc)))
            .ToListAsync(ct);

        HashSet<DateTimeOffset> starts = [];
        foreach (Booking booking in bookings)
        {
            starts.Add(booking.StartUtc);
            if (booking.Status == BookingStatuses.RescheduleRequested && booking.RequestedStartUtc is { } requestedStart)
            {
                starts.Add(requestedStart);
            }
        }

        return starts;
    }

    private async Task EnsureSlotStillFreeAsync(
        AppDbContext db,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        DateTimeOffset nowUtc,
        string? ignoredBookingId,
        CancellationToken ct)
    {
        bool taken = await db.Bookings
            .Where(b => ignoredBookingId == null || b.Id != ignoredBookingId)
            .AnyAsync(b =>
                (b.StartUtc == startUtc || b.RequestedStartUtc == startUtc) &&
                (b.Status == BookingStatuses.Accepted ||
                    b.Status == BookingStatuses.Confirmed ||
                    b.Status == BookingStatuses.Rescheduled ||
                    b.Status == BookingStatuses.RescheduleRequested ||
                    (b.Status == BookingStatuses.Pending && (b.ExpiresAtUtc == null || b.ExpiresAtUtc > nowUtc))), ct);

        if (taken)
        {
            throw new InvalidOperationException("That slot has already been requested.");
        }

        List<BusyInterval> busy = await GetMergedBusyAsync(startUtc, endUtc, ct);
        if (busy.Any(b => b.StartUtc < endUtc && b.EndUtc > startUtc))
        {
            throw new InvalidOperationException("That slot is no longer free.");
        }
    }

    private async Task EnsureAntiSpamAsync(AppDbContext db, string email, string ipHash, DateTimeOffset nowUtc, CancellationToken ct)
    {
        BookingAntiSpamOptions antiSpam = opt.AntiSpam;

        if (antiSpam.MaxPendingPerEmail > 0)
        {
            int pendingCount = await db.Bookings.CountAsync(b =>
                b.Email == email &&
                (b.Status == BookingStatuses.Pending || b.Status == BookingStatuses.RescheduleRequested) &&
                (b.ExpiresAtUtc == null || b.ExpiresAtUtc > nowUtc), ct);

            if (pendingCount >= antiSpam.MaxPendingPerEmail)
            {
                throw new InvalidOperationException("You already have pending requests. Please wait for a reply before sending another one.");
            }
        }

        if (antiSpam.MaxPendingPerIp > 0)
        {
            int pendingCountFromIp = await db.Bookings.CountAsync(b =>
                b.IpHash == ipHash &&
                (b.Status == BookingStatuses.Pending || b.Status == BookingStatuses.RescheduleRequested) &&
                (b.ExpiresAtUtc == null || b.ExpiresAtUtc > nowUtc), ct);

            if (pendingCountFromIp >= antiSpam.MaxPendingPerIp)
            {
                throw new InvalidOperationException("Too many pending requests were already sent from this network.");
            }
        }

        if (antiSpam.CooldownMinutesPerEmail > 0)
        {
            DateTimeOffset cooldownCutoff = nowUtc.AddMinutes(-antiSpam.CooldownMinutesPerEmail);
            bool tooRecent = await db.Bookings.AnyAsync(b => b.Email == email && b.CreatedAtUtc >= cooldownCutoff, ct);
            if (tooRecent)
            {
                throw new InvalidOperationException("Please wait a little before sending another request.");
            }
        }

        if (antiSpam.CooldownMinutesPerIp > 0)
        {
            DateTimeOffset cooldownCutoff = nowUtc.AddMinutes(-antiSpam.CooldownMinutesPerIp);
            bool tooRecentFromIp = await db.Bookings.AnyAsync(b => b.IpHash == ipHash && b.CreatedAtUtc >= cooldownCutoff, ct);
            if (tooRecentFromIp)
            {
                throw new InvalidOperationException("Please wait a little before sending another request from this network.");
            }
        }

        if (antiSpam.MaxRequestsPerEmailPerDay > 0)
        {
            DateTimeOffset dayCutoff = nowUtc.AddDays(-1);
            int lastDayCount = await db.Bookings.CountAsync(b => b.Email == email && b.CreatedAtUtc >= dayCutoff, ct);
            if (lastDayCount >= antiSpam.MaxRequestsPerEmailPerDay)
            {
                throw new InvalidOperationException("Too many requests were sent from this email address today.");
            }
        }

        if (antiSpam.MaxRequestsPerIpPerDay > 0)
        {
            DateTimeOffset dayCutoff = nowUtc.AddDays(-1);
            int lastDayCountFromIp = await db.Bookings.CountAsync(b => b.IpHash == ipHash && b.CreatedAtUtc >= dayCutoff, ct);
            if (lastDayCountFromIp >= antiSpam.MaxRequestsPerIpPerDay)
            {
                throw new InvalidOperationException("Too many requests were sent from this network today.");
            }
        }
    }

    private async Task ExpirePendingAsync(AppDbContext db, DateTimeOffset nowUtc, CancellationToken ct)
    {
        List<Booking> expired = await db.Bookings
            .Where(b =>
                (b.Status == BookingStatuses.Pending || b.Status == BookingStatuses.RescheduleRequested) &&
                b.ExpiresAtUtc != null &&
                b.ExpiresAtUtc <= nowUtc)
            .ToListAsync(ct);

        foreach (Booking booking in expired)
        {
            if (booking.Status == BookingStatuses.RescheduleRequested && !string.IsNullOrWhiteSpace(booking.CalendarEventId))
            {
                booking.RequestedStartUtc = null;
                booking.RequestedEndUtc = null;
                booking.Status = BookingStatuses.Accepted;
            }
            else
            {
                booking.Status = BookingStatuses.Expired;
            }

            booking.UpdatedAtUtc = nowUtc;
        }
    }

    private void ValidatePublicSlot(DateTimeOffset startUtc, DateTimeOffset endUtc, DateTimeOffset nowUtc)
    {
        if (startUtc < nowUtc.AddHours(opt.MinNoticeHours))
        {
            throw new InvalidOperationException("That slot is too soon.");
        }

        if (startUtc > nowUtc.AddDays(opt.HorizonDays))
        {
            throw new InvalidOperationException("That slot is too far in the future.");
        }

        TimeZoneInfo tz = Tz;
        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(startUtc, tz);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(endUtc, tz);
        DateOnly date = DateOnly.FromDateTime(localStart.DateTime);

        if (!opt.OpenDays.Contains((int)date.DayOfWeek))
        {
            throw new InvalidOperationException("That day is not open for booking.");
        }

        if (localStart.Minute % opt.SlotMinutes != 0)
        {
            throw new InvalidOperationException("Invalid slot boundary.");
        }

        DateTime localWorkStart = new(localStart.Year, localStart.Month, localStart.Day, opt.WorkdayStartHour, 0, 0);
        DateTime localWorkEnd = new(localStart.Year, localStart.Month, localStart.Day, opt.WorkdayEndHour, 0, 0);

        if (localStart.DateTime < localWorkStart || localEnd.DateTime > localWorkEnd)
        {
            throw new InvalidOperationException("That slot is outside booking hours.");
        }
    }

    private ICalendarProvider PrimaryProvider()
    {
        return providers.FirstOrDefault(p => p.IsPrimary)
            ?? providers.First();
    }

    private static bool CanVisitorCancel(string status)
    {
        return status is BookingStatuses.Pending
            or BookingStatuses.Accepted
            or BookingStatuses.RescheduleRequested
            or BookingStatuses.Confirmed
            or BookingStatuses.Rescheduled;
    }

    private static bool CanOwnerCancel(string status)
    {
        return status is BookingStatuses.Pending
            or BookingStatuses.Accepted
            or BookingStatuses.RescheduleRequested
            or BookingStatuses.Confirmed
            or BookingStatuses.Rescheduled;
    }

    private static bool CanOwnerReschedule(string status)
    {
        return status is BookingStatuses.Pending
            or BookingStatuses.Accepted
            or BookingStatuses.RescheduleRequested
            or BookingStatuses.Confirmed
            or BookingStatuses.Rescheduled;
    }

    private static string SlotId(DateTimeOffset startUtc) => startUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static bool TryParseSlot(string slotId, out DateTimeOffset startUtc)
    {
        return DateTimeOffset.TryParse(slotId, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out startUtc);
    }

    private static DateTimeOffset LocalToUtc(DateOnly date, int hour, int minute, TimeZoneInfo tz)
    {
        DateTime localUnspecified = new(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
        DateTimeOffset local = new(localUnspecified, tz.GetUtcOffset(localUnspecified));
        return local.ToUniversalTime();
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Utc; }
    }

    private static string NormalizeName(string value)
    {
        string name = value.Trim();
        if (name.Length < 2) throw new InvalidOperationException("Name is required.");
        if (name.Length > 120) throw new InvalidOperationException("Name is too long.");
        return name;
    }

    private static string NormalizeEmail(string value)
    {
        string email = value.Trim().ToLowerInvariant();
        if (email.Length is < 6 or > 200)
        {
            throw new InvalidOperationException("A valid email is required.");
        }

        try
        {
            MailAddress parsed = new(email);
            if (!string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A valid email is required.");
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("A valid email is required.");
        }

        string domain = email.Split('@').Last();
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
        {
            throw new InvalidOperationException("A valid email is required.");
        }

        return email;
    }

    private static string NormalizeVerificationCode(string value)
    {
        string code = new(value.Where(char.IsDigit).ToArray());
        if (code.Length != 6)
        {
            throw new InvalidOperationException("Invalid verification code.");
        }

        return code;
    }

    private async Task EnsureVerifiedEmailTokenAsync(
        AppDbContext db,
        string email,
        string token,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Please verify your email before sending the request.");
        }

        EmailVerification verification = await db.EmailVerifications.FirstOrDefaultAsync(v =>
            v.Email == email &&
            v.VerificationToken == token &&
            v.VerifiedAtUtc != null &&
            v.ConsumedAtUtc == null, ct)
            ?? throw new InvalidOperationException("Please verify your email before sending the request.");

        if (verification.ExpiresAtUtc <= nowUtc)
        {
            throw new InvalidOperationException("Your email verification has expired. Please verify your email again.");
        }

        verification.ConsumedAtUtc = nowUtc;
    }

    private static string NormalizeIpHash(string value)
    {
        string ipHash = value.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(ipHash) ? "unknown" : ipHash;
    }

    private static string HashEmailCode(string email, string code)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{email}:{code}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? NormalizeMeetingLocation(MeetingKind kind, string? value)
    {
        string location = (value ?? string.Empty).Trim();

        if (kind != MeetingKind.InPerson)
        {
            return string.IsNullOrWhiteSpace(location) ? null : location.Length <= 300 ? location : throw new InvalidOperationException("Meeting location is too long.");
        }

        if (location.Length < 5)
        {
            throw new InvalidOperationException("Meeting location is required for an in-person meeting.");
        }

        if (location.Length > 300)
        {
            throw new InvalidOperationException("Meeting location is too long.");
        }

        return location;
    }

    private static string NormalizeMessage(string value)
    {
        string message = value.Trim();
        if (message.Length < 10)
        {
            throw new InvalidOperationException("Please add a short topic for the meeting.");
        }

        if (message.Length > 2000)
        {
            throw new InvalidOperationException("The topic is too long.");
        }

        return message;
    }

    private static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static BookingResponse ToResponse(Booking b) =>
        new(b.Id, b.ManageToken, b.StartUtc, b.EndUtc, b.Kind, b.Status);

    private static BookingView ToView(Booking b) =>
        new(b.Id, b.StartUtc, b.EndUtc, b.Kind, b.Status, b.Name, b.Email, b.Message, b.MeetingLocation, b.RequestedStartUtc, b.RequestedEndUtc);

    private static BookingDecisionView ToDecisionView(Booking b) =>
        new(b.Id, b.StartUtc, b.EndUtc, b.Kind, b.Status, b.Name, b.Email, b.Message, b.MeetingLocation, b.ExpiresAtUtc, b.RequestedStartUtc, b.RequestedEndUtc);
}

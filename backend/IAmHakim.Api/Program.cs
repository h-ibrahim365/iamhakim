using IAmHakim.Api.Data;
using IAmHakim.Api.Hubs;
using IAmHakim.Api.Models;
using IAmHakim.Api.Services;
using IAmHakim.Api.Security;
using IAmHakim.Api.Mail;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Keep the public API contract aligned with the Angular client.
    // MeetingKind is sent as "Video", "Call" or "InPerson", not as enum integers.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
});
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.Configure<ClientIdentityOptions>(builder.Configuration.GetSection("Security:ClientIdentity"));
builder.Services.Configure<TurnstileOptions>(builder.Configuration.GetSection("Security:Turnstile"));
builder.Services.AddSingleton<ClientIdentityService>();
builder.Services.AddHttpClient<TurnstileVerifier>();
builder.Services.AddSingleton<LiveConnectionTracker>();
builder.Services.AddSingleton<SiteStatsService>();

// --- privacy / retention --------------------------------------------
// Background sweep that enforces the booking retention promise (12 months
// post-meeting). Tunable via the "BookingRetention" config section.
builder.Services.Configure<IAmHakim.Api.Services.BookingRetentionOptions>(builder.Configuration.GetSection("BookingRetention"));
builder.Services.AddHostedService<IAmHakim.Api.Services.BookingRetentionService>();

// --- booking / calendar ---------------------------------------------
builder.Services.Configure<IAmHakim.Api.Calendar.BookingOptions>(builder.Configuration.GetSection("Booking"));
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<BookingEmailService>();

if (builder.Configuration.GetValue<bool>("Mail:Enabled"))
{
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}

string bookingMode = builder.Configuration["Booking:Mode"] ?? "mock";
if (string.Equals(bookingMode, "live", StringComparison.OrdinalIgnoreCase))
{
    bool googleEnabled = builder.Configuration.GetValue<bool>("Booking:Google:Enabled");
    bool graphEnabled = builder.Configuration.GetValue<bool>("Booking:Graph:Enabled");
    if (googleEnabled) builder.Services.AddSingleton<IAmHakim.Api.Calendar.ICalendarProvider, IAmHakim.Api.Calendar.GoogleCalendarProvider>();
    if (graphEnabled) builder.Services.AddSingleton<IAmHakim.Api.Calendar.ICalendarProvider, IAmHakim.Api.Calendar.GraphCalendarProvider>();
    if (!googleEnabled && !graphEnabled)
        builder.Services.AddSingleton<IAmHakim.Api.Calendar.ICalendarProvider, IAmHakim.Api.Calendar.MockCalendarProvider>();
}
else
{
    builder.Services.AddSingleton<IAmHakim.Api.Calendar.ICalendarProvider, IAmHakim.Api.Calendar.MockCalendarProvider>();
}
builder.Services.AddScoped<IAmHakim.Api.Calendar.BookingService>();

string? connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'. " +
        "Set it in appsettings or via the environment variable ConnectionStrings__Default.");
}

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

WebApplication app = builder.Build();

app.UseForwardedHeaders();

DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

using (IServiceScope scope = app.Services.CreateScope())
{
    SiteStatsService siteStatsService = scope.ServiceProvider.GetRequiredService<SiteStatsService>();
    await siteStatsService.EnsureCreatedAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("LocalFrontend");
}

if (builder.Configuration.GetValue<bool>("HttpsRedirection:Enabled"))
{
    app.UseHttpsRedirection();
}

app.MapHub<LiveHub>("/hubs/live");

app.MapGet("/api/public-config", (IOptions<TurnstileOptions> turnstileOptions) =>
{
    TurnstileOptions turnstile = turnstileOptions.Value;
    bool turnstileEnabled = turnstile.Enabled && !string.IsNullOrWhiteSpace(turnstile.SiteKey);

    return Results.Ok(new PublicConfigResponse(
        new TurnstilePublicConfig(turnstileEnabled, turnstileEnabled ? turnstile.SiteKey : string.Empty)));
});

app.MapGet("/api/health", async (SiteStatsService statsService, LiveConnectionTracker tracker, CancellationToken cancellationToken) =>
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    bool canConnectToDatabase = await statsService.CanConnectAsync(cancellationToken);
    stopwatch.Stop();

    string status = canConnectToDatabase ? "connected" : "degraded";

    return Results.Ok(new HealthResponse(
        Status: status,
        Api: "up",
        Database: canConnectToDatabase ? "up" : "down",
        Realtime: "up",
        LiveClients: tracker.Count,
        UptimeSeconds: (long)(DateTimeOffset.UtcNow - startedAtUtc).TotalSeconds,
        LatencyMs: stopwatch.ElapsedMilliseconds,
        ServerTimeUtc: DateTimeOffset.UtcNow));
});

app.MapGet("/api/stats", async (SiteStatsService statsService, CancellationToken cancellationToken) =>
{
    return Results.Ok(await statsService.GetStatsAsync(cancellationToken));
});

app.MapGet("/api/events", async (SiteStatsService statsService, int? limit, CancellationToken cancellationToken) =>
{
    return Results.Ok(await statsService.GetRecentEventsAsync(limit ?? 10, cancellationToken));
});

app.MapPost("/api/visit", async (SiteStatsService statsService, IHubContext<LiveHub> hubContext, CancellationToken cancellationToken) =>
{
    StatsResponse stats = await statsService.RegisterVisitAsync(cancellationToken);
    await hubContext.Clients.All.SendAsync("statsUpdated", stats, cancellationToken);
    await hubContext.Clients.All.SendAsync("timelineEvent", new SiteEventResponse(0, "visit", "New portfolio visit recorded", DateTimeOffset.UtcNow), cancellationToken);
    return Results.Ok(stats);
});

app.MapPost("/api/up", async (SiteStatsService statsService, IHubContext<LiveHub> hubContext, CancellationToken cancellationToken) =>
{
    StatsResponse stats = await statsService.RegisterUpClickAsync(cancellationToken);
    SiteEventResponse siteEvent = new(0, "up", "Someone pressed UP", DateTimeOffset.UtcNow);

    await hubContext.Clients.All.SendAsync("statsUpdated", stats, cancellationToken);
    await hubContext.Clients.All.SendAsync("timelineEvent", siteEvent, cancellationToken);

    return Results.Ok(new UpResponse("UP registered", stats));
});

// Generic site-wide click. Throttled client-side. No timeline event (too noisy).
app.MapPost("/api/click", async (SiteStatsService statsService, IHubContext<LiveHub> hubContext, CancellationToken cancellationToken) =>
{
    StatsResponse stats = await statsService.RegisterClickAsync(cancellationToken);
    await hubContext.Clients.All.SendAsync("statsUpdated", stats, cancellationToken);
    return Results.Ok(stats);
});

app.MapPost("/api/algo-run", async (AlgoRunRequest? request, SiteStatsService statsService, IHubContext<LiveHub> hubContext, CancellationToken cancellationToken) =>
{
    string outcome = request?.Outcome == "no-path" ? "no-path" : "found";
    int expanded = Math.Clamp(request?.Expanded ?? 0, 0, 100000);
    bool maze = request?.Maze ?? false;

    StatsResponse stats = await statsService.RegisterAlgoRunAsync(outcome, expanded, maze, cancellationToken);
    SiteEventResponse siteEvent = new(0, maze ? "maze" : "algo", $"A* visualised · {(outcome == "no-path" ? "no path" : "path found")}", DateTimeOffset.UtcNow);

    await hubContext.Clients.All.SendAsync("statsUpdated", stats, cancellationToken);
    await hubContext.Clients.All.SendAsync("timelineEvent", siteEvent, cancellationToken);

    return Results.Ok(stats);
});

app.MapPost("/api/flow/simulate", async (SiteStatsService statsService, IHubContext<LiveHub> hubContext, CancellationToken cancellationToken) =>
{
    string correlationId = $"flow-{Guid.NewGuid():N}"[..18];
    SiteEventResponse siteEvent = await statsService.RegisterFlowEventAsync(correlationId, "Backend flow simulation completed", cancellationToken);

    await hubContext.Clients.All.SendAsync("timelineEvent", siteEvent, cancellationToken);

    return Results.Ok(new FlowSimulationResponse(correlationId, "Flow simulation persisted", DateTimeOffset.UtcNow));
});

app.MapGet("/api/availability", async (IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    return Results.Ok(await booking.GetAvailabilityAsync(cancellationToken));
});


app.MapGet("/api/address-search", async (string? q, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    string query = (q ?? string.Empty).Trim();
    if (query.Length < 3)
    {
        return Results.Ok(Array.Empty<AddressSuggestionResponse>());
    }

    if (query.Length > 120)
    {
        query = query[..120];
    }

    HttpClient client = httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.UserAgent.ParseAdd("iamhakim.com-booking/1.0 (+https://iamhakim.com)");

    string url = "https://nominatim.openstreetmap.org/search" +
        $"?format=jsonv2&addressdetails=1&limit=8&countrycodes=be&q={Uri.EscapeDataString(query)}";

    try
    {
        using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Results.Ok(Array.Empty<AddressSuggestionResponse>());
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        List<AddressSuggestionResponse> suggestions = [];
        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string? label = item.TryGetProperty("display_name", out JsonElement displayName)
                ? displayName.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            if (!IsBelgianAddressResult(item))
            {
                continue;
            }

            suggestions.Add(new AddressSuggestionResponse(
                Label: label,
                Latitude: item.TryGetProperty("lat", out JsonElement lat) ? lat.GetString() : null,
                Longitude: item.TryGetProperty("lon", out JsonElement lon) ? lon.GetString() : null));
        }

        return Results.Ok(suggestions);
    }
    catch
    {
        return Results.Ok(Array.Empty<AddressSuggestionResponse>());
    }
});

app.MapPost("/api/bookings/email-verification/request", async (
    EmailVerificationRequest request,
    HttpContext httpContext,
    ClientIdentityService clientIdentityService,
    TurnstileVerifier turnstileVerifier,
    IAmHakim.Api.Calendar.BookingService booking,
    CancellationToken cancellationToken) =>
{
    ClientIdentity clientIdentity = clientIdentityService.Get(httpContext);
    TurnstileVerificationResult turnstile = await turnstileVerifier.VerifyAsync(request.TurnstileToken, clientIdentity.IpAddress, cancellationToken);

    if (!turnstile.Success)
    {
        return Results.BadRequest(new { code = turnstile.ErrorCode, error = turnstile.ErrorMessage });
    }

    try
    {
        return Results.Ok(await booking.RequestEmailVerificationAsync(request, clientIdentity.IpHash, cancellationToken));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { code = BookingErrorCode(ex.Message), error = ex.Message });
    }
});

app.MapPost("/api/bookings/email-verification/confirm", async (EmailVerificationConfirmRequest request, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await booking.ConfirmEmailVerificationAsync(request, cancellationToken));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { code = BookingErrorCode(ex.Message), error = ex.Message });
    }
});

app.MapPost("/api/bookings", async (
    BookingRequest request,
    HttpContext httpContext,
    ClientIdentityService clientIdentityService,
    IAmHakim.Api.Calendar.BookingService booking,
    IHubContext<LiveHub> hubContext,
    CancellationToken cancellationToken) =>
{
    ClientIdentity clientIdentity = clientIdentityService.Get(httpContext);

    try
    {
        BookingResponse response = await booking.CreateBookingAsync(request, clientIdentity.IpHash, cancellationToken);
        await hubContext.Clients.All.SendAsync("timelineEvent", new SiteEventResponse(0, "booking", "A meeting request was sent", DateTimeOffset.UtcNow), cancellationToken);
        return Results.Ok(new { booking = response, manageUrl = booking_ManageUrl(booking, response.ManageToken) });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { code = BookingErrorCode(ex.Message), error = ex.Message });
    }

    static string booking_ManageUrl(IAmHakim.Api.Calendar.BookingService svc, string token) => svc.BuildManageUrl(token);
});

app.MapGet("/api/bookings/admin", async (string token, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    BookingDecisionView? view = await booking.GetDecisionByTokenAsync(token, cancellationToken);
    if (view is null)
    {
        return Results.Content(AdminPage("Demande introuvable", "Le lien est invalide ou la demande n'existe plus."), "text/html; charset=utf-8");
    }

    AvailabilityResponse availability = await booking.GetAvailabilityAsync(cancellationToken);
    return Results.Content(AdminDecisionPage(view, token, availability), "text/html; charset=utf-8");
});

app.MapPost("/api/bookings/admin/accept", async (HttpRequest request, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    IFormCollection form = await request.ReadFormAsync(cancellationToken);
    string token = form["token"].ToString();

    try
    {
        BookingResponse response = await booking.AcceptAsync(token, cancellationToken);
        return Results.Content(AdminPage("Demande acceptée", $"Le rendez-vous est confirmé. Statut : {WebUtility.HtmlEncode(response.Status)}."), "text/html; charset=utf-8");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Content(AdminPage("Action impossible", WebUtility.HtmlEncode(ex.Message)), "text/html; charset=utf-8");
    }
});

app.MapPost("/api/bookings/admin/reject", async (HttpRequest request, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    IFormCollection form = await request.ReadFormAsync(cancellationToken);
    string token = form["token"].ToString();

    try
    {
        BookingResponse response = await booking.RejectAsync(token, cancellationToken);
        return Results.Content(AdminPage("Demande refusée", $"Le créneau est libéré. Statut : {WebUtility.HtmlEncode(response.Status)}."), "text/html; charset=utf-8");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Content(AdminPage("Action impossible", WebUtility.HtmlEncode(ex.Message)), "text/html; charset=utf-8");
    }
});

app.MapPost("/api/bookings/admin/cancel", async (HttpRequest request, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    IFormCollection form = await request.ReadFormAsync(cancellationToken);
    string token = form["token"].ToString();

    try
    {
        BookingResponse response = await booking.AdminCancelAsync(token, cancellationToken);
        return Results.Content(AdminPage("Rendez-vous annulé", $"Le créneau est libéré. Statut : {WebUtility.HtmlEncode(response.Status)}."), "text/html; charset=utf-8");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Content(AdminPage("Action impossible", WebUtility.HtmlEncode(ex.Message)), "text/html; charset=utf-8");
    }
});

app.MapPost("/api/bookings/admin/reschedule", async (HttpRequest request, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    IFormCollection form = await request.ReadFormAsync(cancellationToken);
    string token = form["token"].ToString();
    string newSlotId = NormalizeAdminSlotInput(form["newSlotId"].ToString());

    try
    {
        BookingResponse response = await booking.AdminRescheduleAsync(token, newSlotId, cancellationToken);
        return Results.Content(AdminPage("Horaire modifié", $"Le rendez-vous a été déplacé. Statut : {WebUtility.HtmlEncode(response.Status)}."), "text/html; charset=utf-8");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Content(AdminPage("Action impossible", WebUtility.HtmlEncode(ex.Message)), "text/html; charset=utf-8");
    }
});


app.MapGet("/book/manage", (string token, IAmHakim.Api.Calendar.BookingService booking) =>
{
    string target = booking.BuildFrontendRouteUrl("/book/manage", $"token={Uri.EscapeDataString(token)}");
    return Results.Redirect(target, permanent: false);
});

app.MapGet("/api/bookings/manage", async (string token, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    BookingView? view = await booking.GetByTokenAsync(token, cancellationToken);
    return view is null ? Results.NotFound() : Results.Ok(view);
});

app.MapPost("/api/bookings/manage", async (ManageBookingRequest request, IAmHakim.Api.Calendar.BookingService booking, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await booking.ManageAsync(request, cancellationToken));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { code = BookingErrorCode(ex.Message), error = ex.Message });
    }
});

static bool IsBelgianAddressResult(JsonElement item)
{
    if (!item.TryGetProperty("address", out JsonElement address))
    {
        return true;
    }

    if (!address.TryGetProperty("country_code", out JsonElement countryCode))
    {
        return true;
    }

    return string.Equals(countryCode.GetString(), "be", StringComparison.OrdinalIgnoreCase);
}

static string BookingErrorCode(string message)
{
    return message switch
    {
        "A verification code was just sent. Please wait a minute before requesting another one." => "email_code_recently_sent",
        "A verification code was just sent from this network. Please wait a minute before requesting another one." => "email_code_recently_sent_ip",
        "Too many verification codes were requested for this email address. Please try again later." => "email_code_too_many_requests",
        "Too many verification codes were requested from this network. Please try again later." => "email_code_too_many_ip_requests",
        "Too many verification codes were requested from this network today. Please try again later." => "email_code_too_many_ip_requests",
        "Unknown verification request." => "email_verification_unknown",
        "This verification code has expired. Please request a new one." => "email_code_expired",
        "Too many invalid attempts. Please request a new verification code." => "email_code_too_many_attempts",
        "Invalid verification code." => "email_code_invalid",
        "Please verify your email before sending the request." => "email_not_verified",
        "Your email verification has expired. Please verify your email again." => "email_verification_expired",
        "A valid email is required." => "email_invalid",

        "Invalid slot." => "slot_invalid",
        "That slot has already been requested." => "slot_already_requested",
        "That slot is no longer free." => "slot_not_free",
        "That slot is too soon." => "slot_too_soon",
        "That slot is too far in the future." => "slot_too_far",
        "That day is not open for booking." => "day_not_open",
        "Invalid slot boundary." => "slot_invalid_boundary",
        "That slot is outside booking hours." => "slot_outside_hours",
        "Invalid new slot." => "slot_invalid",
        "Invalid reschedule request." => "booking_invalid_reschedule",

        "You already have pending requests. Please wait for a reply before sending another one." => "pending_limit",
        "Please wait a little before sending another request." => "request_cooldown",
        "Too many pending requests were already sent from this network." => "ip_pending_limit",
        "Please wait a little before sending another request from this network." => "ip_request_cooldown",
        "Too many requests were sent from this email address today." => "daily_limit",
        "Too many requests were sent from this network today." => "ip_daily_limit",

        "Name is required." => "name_required",
        "Name is too long." => "name_too_long",
        "Please add a short topic for the meeting." => "topic_required",
        "The topic is too long." => "topic_too_long",
        "Meeting location is required for an in-person meeting." => "meeting_location_required",
        "Meeting location is too long." => "meeting_location_too_long",

        "Unknown booking request." => "booking_unknown_request",
        "Unknown booking." => "booking_unknown",
        "This request has expired." => "booking_request_expired",
        "Unknown action." => "booking_unknown_action",

        _ when message.StartsWith("This request is already ", StringComparison.OrdinalIgnoreCase) => "booking_already_processed",
        _ when message.StartsWith("Resend failed with ", StringComparison.OrdinalIgnoreCase) => "mail_send_failed",
        _ when message.StartsWith("Google Calendar ", StringComparison.OrdinalIgnoreCase) => "calendar_failed",
        _ when message.StartsWith("Cannot update a Google Calendar event", StringComparison.OrdinalIgnoreCase) => "calendar_failed",
        _ => "unknown"
    };
}

static string AdminDecisionPage(BookingDecisionView booking, string token, AvailabilityResponse availability)
{
    string when = WebUtility.HtmlEncode(FormatAdminWhen(booking.StartUtc));
    string requestedWhen = booking.RequestedStartUtc is null
        ? ""
        : WebUtility.HtmlEncode(FormatAdminWhen(booking.RequestedStartUtc.Value));
    string name = WebUtility.HtmlEncode(booking.Name);
    string email = WebUtility.HtmlEncode(booking.Email);
    string message = WebUtility.HtmlEncode(booking.Message ?? "");
    string meetingLocation = WebUtility.HtmlEncode(booking.MeetingLocation ?? "");
    string status = WebUtility.HtmlEncode(booking.Status);
    string kind = WebUtility.HtmlEncode(FormatAdminKind(booking.Kind));
    string safeToken = WebUtility.HtmlEncode(token);
    string slotOptions = AdminAvailabilityOptions(availability, booking);

    string body =
        "<div class=\"meta\">" +
        AdminRow("Nom", name) +
        AdminRow("E-mail", $"<a href=\"mailto:{email}\">{email}</a>") +
        AdminRow("Date", when) +
        (string.IsNullOrWhiteSpace(requestedWhen) ? "" : AdminRow("Nouvel horaire demandé", requestedWhen)) +
        AdminRow("Type", kind) +
        (string.IsNullOrWhiteSpace(meetingLocation) ? "" : AdminRow("Lieu demandé", meetingLocation)) +
        AdminRow("Statut", AdminBadge(status)) +
        AdminRow("Sujet", message) +
        "</div>";

    if (booking.Status == BookingStatuses.Pending || booking.Status == BookingStatuses.RescheduleRequested)
    {
        body +=
            "<div class=\"actions\">" +
            "<form method=\"post\" action=\"/api/bookings/admin/accept\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{safeToken}\">" +
            "<button class=\"accept\" type=\"submit\">Accepter</button>" +
            "</form>" +
            "<form method=\"post\" action=\"/api/bookings/admin/reject\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{safeToken}\">" +
            "<button class=\"reject\" type=\"submit\">Refuser</button>" +
            "</form>" +
            "</div>";
    }

    if (booking.Status == BookingStatuses.Accepted || booking.Status == BookingStatuses.RescheduleRequested || booking.Status == BookingStatuses.Pending)
    {
        body +=
            "<div class=\"admin-tools\">" +
            "<h2>Gestion</h2>" +
            "<p>Tu peux annuler ou déplacer le rendez-vous depuis cette page. Le calendrier et la DB restent synchronisés.</p>" +
            "<form class=\"reschedule-form\" method=\"post\" action=\"/api/bookings/admin/reschedule\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{safeToken}\">" +
            "<label>Nouvel horaire <span>mardi, mercredi, vendredi ou samedi · 18:00-21:00 · Bruxelles</span></label>" +
            "<select name=\"newSlotId\" required>" +
            slotOptions +
            "</select>" +
            "<button class=\"neutral\" type=\"submit\">Déplacer</button>" +
            "</form>" +
            "<form method=\"post\" action=\"/api/bookings/admin/cancel\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{safeToken}\">" +
            "<button class=\"danger\" type=\"submit\">Annuler le rendez-vous</button>" +
            "</form>" +
            "</div>";
    }
    else
    {
        body += "<p class=\"sub\">Cette demande n’attend plus de décision.</p>";
    }

    string subtitle = booking.Status == BookingStatuses.RescheduleRequested
        ? "Un changement d’horaire est demandé. L’ancien rendez-vous reste actif tant que tu n’acceptes pas."
        : booking.Status == BookingStatuses.Pending
            ? "Une demande de rendez-vous vérifiée attend ta décision."
            : "Gestion du rendez-vous.";

    return AdminPage("Gestion booking", body, subtitle);
}

static string AdminAvailabilityOptions(AvailabilityResponse availability, BookingDecisionView currentBooking)
{
    List<AvailabilitySlot> slots = availability.Days
        .SelectMany(day => day.Slots)
        .Where(slot => slot.Available && slot.StartUtc != currentBooking.StartUtc && slot.StartUtc != currentBooking.RequestedStartUtc)
        .OrderBy(slot => slot.StartUtc)
        .Take(80)
        .ToList();

    if (slots.Count == 0)
    {
        return "<option value=\"\" disabled selected>Aucun créneau disponible</option>";
    }

    StringBuilder builder = new();
    builder.Append("<option value=\"\" disabled selected>Choisir un créneau disponible</option>");

    foreach (AvailabilitySlot slot in slots)
    {
        string value = WebUtility.HtmlEncode(slot.Id);
        string label = WebUtility.HtmlEncode(FormatAdminWhen(slot.StartUtc));
        builder.Append($"<option value=\"{value}\">{label}</option>");
    }

    return builder.ToString();
}

static string NormalizeAdminSlotInput(string raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return raw;

    if (raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || raw.Contains('+'))
    {
        return raw;
    }

    if (!DateTime.TryParse(raw, out DateTime local))
    {
        return raw;
    }

    try
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
        DateTime unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        return utc.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
    catch
    {
        return DateTime.SpecifyKind(local, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}

static string FormatAdminWhen(DateTimeOffset utc)
{
    try
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, tz);
        string formatted = local.ToString("dddd d MMMM yyyy 'à' HH:mm", System.Globalization.CultureInfo.GetCultureInfo("fr-BE"));
        return char.ToUpper(formatted[0], System.Globalization.CultureInfo.GetCultureInfo("fr-BE")) + formatted[1..];
    }
    catch
    {
        return utc.UtcDateTime.ToString("dd/MM/yyyy HH:mm 'UTC'");
    }
}

static string FormatAdminKind(MeetingKind kind)
{
    return kind switch
    {
        MeetingKind.Video => "Visio",
        MeetingKind.Call => "Appel",
        MeetingKind.InPerson => "En personne",
        _ => kind.ToString()
    };
}

static string AdminRow(string label, string value)
{
    return $"<div class=\"row\"><span>{WebUtility.HtmlEncode(label)}</span><strong>{value}</strong></div>";
}

static string AdminBadge(string value)
{
    return $"<em>{value}</em>";
}

static string AdminPage(string title, string body, string? subtitle = null)
{
    string safeTitle = WebUtility.HtmlEncode(title);
    string safeSubtitle = WebUtility.HtmlEncode(subtitle ?? "");

    return "<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        $"<title>{safeTitle}</title>" +
        "<style>" +
        ":root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;min-height:100vh;background:radial-gradient(circle at top right,rgba(70,227,208,.12),transparent 34%),#05070d;color:#f7f7f8;font-family:Inter,Segoe UI,Arial,sans-serif}" +
        ".wrap{max-width:800px;margin:0 auto;padding:42px 18px}.card{position:relative;overflow:hidden;border:1px solid #202637;border-radius:26px;background:#0b0f1a;box-shadow:0 28px 90px rgba(0,0,0,.42)}" +
        ".bar{height:3px;background:linear-gradient(90deg,#f0a92b,#ff5c7a 55%,#46e3d0)}.inner{padding:30px}.eyebrow{color:#46e3d0;font-size:11px;font-weight:800;letter-spacing:.16em;text-transform:uppercase;margin-bottom:12px}" +
        "h1{margin:0;color:#f7f7f8;font-size:32px;line-height:1.12;letter-spacing:-.04em}h2{margin:0 0 8px;font-size:18px}.sub{margin:12px 0 0;color:#a8b1c7;line-height:1.6}.meta{margin-top:26px;border:1px solid #202637;border-radius:20px;background:#090e18;overflow:hidden}" +
        ".row{display:grid;grid-template-columns:160px 1fr;gap:16px;padding:14px 16px;border-top:1px solid #202637}.row:first-child{border-top:0}.row span{color:#8d94a7;font-size:13px}.row strong{color:#f7f7f8;font-size:14px;line-height:1.45}.row a{color:#46e3d0;text-decoration:none}.row em{display:inline-block;padding:5px 9px;border-radius:999px;background:rgba(70,227,208,.08);border:1px solid rgba(70,227,208,.24);color:#46e3d0;font-style:normal;font-size:12px;text-transform:uppercase;letter-spacing:.08em}" +
        ".actions{display:flex;gap:12px;flex-wrap:wrap;margin-top:24px}.admin-tools{margin-top:26px;padding:18px;border:1px solid #202637;border-radius:20px;background:#090e18}.admin-tools p{margin:0 0 16px;color:#a8b1c7;line-height:1.55}.reschedule-form{display:grid;gap:10px;margin-bottom:14px}.reschedule-form label{color:#8d94a7;font-size:13px}.reschedule-form label span{color:#5e687d}.reschedule-form select{min-height:44px;border-radius:12px;border:1px solid #273044;background:#050913;color:#f7f7f8;padding:0 12px}" +
        "button{border:0;border-radius:14px;padding:14px 20px;font-weight:800;cursor:pointer;font-size:14px}.accept{background:linear-gradient(135deg,#f0a92b,#ff5c7a);color:#070a12}.reject,.neutral{background:#111827;color:#f7f7f8;border:1px solid #2b3448}.danger{background:rgba(255,92,122,.12);color:#ff8ba0;border:1px solid rgba(255,92,122,.32)}" +
        ".foot{padding:18px 30px;border-top:1px solid #202637;background:#080c15;color:#8d94a7;font-size:13px}.foot a{color:#46e3d0;text-decoration:none}@media(max-width:560px){.inner{padding:24px}.row{grid-template-columns:1fr;gap:6px}h1{font-size:28px}}" +
        "</style></head>" +
        "<body><main class=\"wrap\"><section class=\"card\"><div class=\"bar\"></div><div class=\"inner\"><div class=\"eyebrow\">Booking admin</div>" +
        $"<h1>{safeTitle}</h1>" +
        (string.IsNullOrWhiteSpace(safeSubtitle) ? "" : $"<p class=\"sub\">{safeSubtitle}</p>") +
        body +
        "</div><div class=\"foot\">Hakim · <a href=\"https://iamhakim.com\">iamhakim.com</a></div></section></main></body></html>";
}

app.Run();

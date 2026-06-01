using IAmHakim.Api.Data;
using IAmHakim.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IAmHakim.Api.Services;

public sealed class SiteStatsService(IDbContextFactory<AppDbContext> dbContextFactory, LiveConnectionTracker liveConnectionTracker)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Apply any pending EF Core migrations. This evolves the schema safely
        // without dropping data, unlike EnsureCreated which ignores existing DBs.
        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnsureSingletonStatsRowAsync(dbContext, cancellationToken);
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task<StatsResponse> GetStatsAsync(CancellationToken cancellationToken)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        SiteStat stat = await EnsureSingletonStatsRowAsync(dbContext, cancellationToken);
        return ToResponse(stat);
    }

    public async Task<IReadOnlyList<SiteEventResponse>> GetRecentEventsAsync(int limit, CancellationToken cancellationToken)
    {
        int safeLimit = Math.Clamp(limit, 1, 30);

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.SiteEvents
            .AsNoTracking()
            .OrderByDescending(siteEvent => siteEvent.CreatedAtUtc)
            .Take(safeLimit)
            .Select(siteEvent => new SiteEventResponse(
                siteEvent.Id,
                siteEvent.Kind,
                siteEvent.Label,
                siteEvent.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<StatsResponse> RegisterVisitAsync(CancellationToken cancellationToken)
    {
        return UpdateStatsAsync(
            kind: "visit",
            label: "New portfolio visit recorded",
            mutation: stat =>
            {
                stat.TotalVisits++;
                stat.LastVisitAtUtc = DateTimeOffset.UtcNow;
            },
            cancellationToken);
    }

    public Task<StatsResponse> RegisterUpClickAsync(CancellationToken cancellationToken)
    {
        return UpdateStatsAsync(
            kind: "up",
            label: "Someone pressed UP",
            mutation: stat =>
            {
                stat.UpClicks++;
                stat.LastUpAtUtc = DateTimeOffset.UtcNow;
            },
            cancellationToken);
    }

    /// <summary>
    /// Records a generic click anywhere on the site. Throttled client-side so this
    /// captures meaningful interactions rather than every micro-event.
    /// Does NOT emit a per-event timeline entry — it would drown the feed.
    /// </summary>
    public async Task<StatsResponse> RegisterClickAsync(CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            SiteStat stat = await EnsureSingletonStatsRowAsync(dbContext, cancellationToken);
            stat.Clicks++;
            stat.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(stat);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task<StatsResponse> RegisterAlgoRunAsync(string outcome, int expanded, bool maze, CancellationToken cancellationToken)
    {
        string safeOutcome = outcome == "no-path" ? "no path" : "path found";
        string label = maze
            ? $"Maze Builder played · {safeOutcome} · score {expanded}"
            : $"A* visualised · {safeOutcome} · {expanded} nodes expanded";
        return UpdateStatsAsync(
            kind: maze ? "maze" : "algo",
            label: label,
            mutation: stat =>
            {
                stat.AlgoRuns++;
                if (maze) stat.UpClicks++;   // UpClicks now tracks Maze Builder plays
            },
            cancellationToken);
    }

    public async Task<SiteEventResponse> RegisterFlowEventAsync(string correlationId, string label, CancellationToken cancellationToken)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        SiteEvent siteEvent = new()
        {
            Kind = "flow",
            Label = $"{label} · {correlationId}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.SiteEvents.Add(siteEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SiteEventResponse(siteEvent.Id, siteEvent.Kind, siteEvent.Label, siteEvent.CreatedAtUtc);
    }

    private async Task<StatsResponse> UpdateStatsAsync(
        string kind,
        string label,
        Action<SiteStat> mutation,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            SiteStat stat = await EnsureSingletonStatsRowAsync(dbContext, cancellationToken);

            mutation(stat);
            stat.UpdatedAtUtc = DateTimeOffset.UtcNow;

            dbContext.SiteEvents.Add(new SiteEvent
            {
                Kind = kind,
                Label = label,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(stat);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<SiteStat> EnsureSingletonStatsRowAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        SiteStat? stat = await dbContext.SiteStats.FirstOrDefaultAsync(item => item.Id == SiteStat.SingletonId, cancellationToken);

        if (stat is not null)
        {
            return stat;
        }

        stat = new SiteStat
        {
            Id = SiteStat.SingletonId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.SiteStats.Add(stat);
        await dbContext.SaveChangesAsync(cancellationToken);

        return stat;
    }

    private StatsResponse ToResponse(SiteStat stat)
    {
        return new StatsResponse(
            stat.TotalVisits,
            stat.UpClicks,
            stat.Clicks,
            stat.AlgoRuns,
            liveConnectionTracker.Count,
            stat.LastVisitAtUtc,
            stat.LastUpAtUtc,
            stat.UpdatedAtUtc);
    }
}

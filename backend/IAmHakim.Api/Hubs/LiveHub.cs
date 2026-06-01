using IAmHakim.Api.Security;
using IAmHakim.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace IAmHakim.Api.Hubs;

public sealed class LiveHub(
    LiveConnectionTracker liveConnectionTracker,
    SiteStatsService siteStatsService,
    ClientIdentityService clientIdentityService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        string clientKey = clientIdentityService.GetLiveClientKey(Context.GetHttpContext(), Context.ConnectionId);
        liveConnectionTracker.Add(Context.ConnectionId, clientKey);

        await Clients.Caller.SendAsync("statsUpdated", await siteStatsService.GetStatsAsync(CancellationToken.None));
        await Clients.All.SendAsync("liveClientsUpdated", liveConnectionTracker.Count);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        liveConnectionTracker.Remove(Context.ConnectionId);
        await Clients.All.SendAsync("liveClientsUpdated", liveConnectionTracker.Count);
        await base.OnDisconnectedAsync(exception);
    }
}

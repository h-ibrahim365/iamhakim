using System.Collections.Concurrent;

namespace IAmHakim.Api.Services;

/// <summary>
/// Tracks live visitors by client key instead of raw SignalR connections. This
/// prevents four tabs from the same visitor/IP from looking like four people.
/// </summary>
public sealed class LiveConnectionTracker
{
    private readonly ConcurrentDictionary<string, string> _connectionToClient = new();
    private readonly ConcurrentDictionary<string, byte> _liveClients = new();

    public int Count => _liveClients.Count;

    public void Add(string connectionId, string clientKey)
    {
        _connectionToClient[connectionId] = clientKey;
        _liveClients.TryAdd(clientKey, 0);
    }

    public void Remove(string connectionId)
    {
        if (!_connectionToClient.TryRemove(connectionId, out string? clientKey))
        {
            return;
        }

        bool stillHasAnotherConnection = _connectionToClient.Any(pair => pair.Value == clientKey);
        if (!stillHasAnotherConnection)
        {
            _liveClients.TryRemove(clientKey, out _);
        }
    }
}

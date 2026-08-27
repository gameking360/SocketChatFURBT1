using System.Collections.Concurrent;
using SocketChat.Net;

namespace SocketChat.Peer;

public enum PeerRegistration
{
    Added,
    Replaced,
    Rejected
}

public sealed class PeerRegistry
{
    private readonly ConcurrentDictionary<PeerEndpoint, PeerConnection> _connections = new();
    private readonly ConcurrentDictionary<PeerEndpoint, byte> _dialing = new();
    private readonly string _selfNickname;

    public PeerRegistry(string selfNickname) => _selfNickname = selfNickname;

    public IReadOnlyCollection<PeerConnection> Connections => _connections.Values.ToArray();

    public IReadOnlyCollection<PeerEndpoint> Endpoints => _connections.Keys.ToArray();

    public bool Knows(PeerEndpoint endpoint) =>
        _connections.ContainsKey(endpoint) || _dialing.ContainsKey(endpoint);

    public bool TryBeginDial(PeerEndpoint endpoint) =>
        !_connections.ContainsKey(endpoint) && _dialing.TryAdd(endpoint, 0);

    public void EndDial(PeerEndpoint endpoint) => _dialing.TryRemove(endpoint, out _);

    public bool IsNicknameTaken(string nickname, PeerEndpoint from) =>
        _connections.Any(entry => entry.Key != from && Matches(entry.Value, nickname));

    public PeerConnection? Find(string nickname) =>
        _connections.Values.FirstOrDefault(connection => Matches(connection, nickname));

    public PeerRegistration Register(PeerConnection connection)
    {
        while (true)
        {
            if (_connections.TryAdd(connection.Endpoint, connection))
                return PeerRegistration.Added;

            if (!_connections.TryGetValue(connection.Endpoint, out var current))
                continue;

            if (!PrefersNew(current, connection))
                return PeerRegistration.Rejected;

            if (_connections.TryUpdate(connection.Endpoint, connection, current))
            {
                current.Close("conexão duplicada");
                return PeerRegistration.Replaced;
            }
        }
    }

    public bool Remove(PeerConnection connection) =>
        _connections.TryRemove(KeyValuePair.Create(connection.Endpoint, connection));

    // Na conexão simultânea os dois lados mantêm a que foi aberta pelo apelido menor.
    private bool PrefersNew(PeerConnection current, PeerConnection candidate)
    {
        if (current.Outbound == candidate.Outbound)
            return false;

        var keepOutbound = string.CompareOrdinal(_selfNickname, candidate.Nickname) < 0;
        return candidate.Outbound == keepOutbound;
    }

    private static bool Matches(PeerConnection connection, string nickname) =>
        string.Equals(connection.Nickname, nickname, StringComparison.OrdinalIgnoreCase);
}

using System.Net;

namespace SocketChat.Net;

public readonly record struct PeerEndpoint(string Host, int Port)
{
    private const string Loopback = "127.0.0.1";

    public override string ToString() => $"{Host}:{Port}";

    public static PeerEndpoint FromSocket(EndPoint? remote, int listenPort)
    {
        var host = remote is IPEndPoint ip ? Normalize(ip.Address.ToString()) : Loopback;
        return new PeerEndpoint(host, listenPort);
    }

    public static bool TryParse(string? value, out PeerEndpoint endpoint)
    {
        endpoint = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Trim().Split(':');
        var host = parts.Length == 1 ? Loopback : parts[0];
        var port = parts[^1];

        if (parts.Length > 2 || !int.TryParse(port, out var number) || number is < 1 or > 65535)
            return false;

        endpoint = new PeerEndpoint(Normalize(host), number);
        return true;
    }

    public static IEnumerable<PeerEndpoint> ParseList(string csv)
    {
        foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParse(item, out var endpoint))
                yield return endpoint;
        }
    }

    public static string ToCsv(IEnumerable<PeerEndpoint> endpoints) => string.Join(',', endpoints);

    private static string Normalize(string host) =>
        host is "localhost" or "::1" or "0.0.0.0" ? Loopback : host;
}

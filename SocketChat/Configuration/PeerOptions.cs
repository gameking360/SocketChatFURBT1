using SocketChat.Net;

namespace SocketChat.Configuration;

public sealed class PeerOptions
{
    public const string Usage = """
        Uso:
          SocketChat <porta> <apelido> [par ...]
          SocketChat --config <arquivo>

        A porta de escuta e os pares aceitam "host:porta" ou apenas "porta"
        (assume 127.0.0.1). Em rede, informe na porta de escuta o IP desta
        máquina, que é o endereço anunciado aos demais.

        Exemplos:
          SocketChat 9001 alice
          SocketChat 9002 bob 9001 127.0.0.1:9003
          SocketChat 192.168.0.10:9001 alice
          SocketChat 192.168.0.20:9002 bob 192.168.0.10:9001
          SocketChat --config exemplos/alice.conf

        Arquivo de configuração:
          porta=192.168.0.10:9001
          apelido=alice
          pares=192.168.0.20:9002,192.168.0.30:9003
        """;

    public required string ListenHost { get; init; }

    public required int ListenPort { get; init; }

    public required string Nickname { get; init; }

    public required IReadOnlyList<PeerEndpoint> KnownPeers { get; init; }

    public static PeerOptions Parse(string[] args)
    {
        if (args.Length >= 2 && args[0] is "--config" or "-c")
            return FromFile(args[1]);

        if (args.Length < 2)
            throw new FormatException("informe a porta de escuta e o apelido.");

        return Create(args[0], args[1], string.Join(',', args.Skip(2)));
    }

    private static PeerOptions FromFile(string path)
    {
        if (!File.Exists(path))
            throw new FormatException($"arquivo de configuração não encontrado: {path}");

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(path))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
                continue;

            var pair = text.Split('=', 2);
            if (pair.Length == 2)
                settings[pair[0].Trim()] = pair[1].Trim();
        }

        return Create(Setting(settings, "porta"), Setting(settings, "apelido"), Setting(settings, "pares"));
    }

    private static PeerOptions Create(string listen, string nickname, string peers)
    {
        // O host informado aqui é o endereço que este nó anuncia aos demais.
        if (!PeerEndpoint.TryParse(listen, out var endpoint))
            throw new FormatException($"porta de escuta inválida: '{listen}'.");

        if (string.IsNullOrWhiteSpace(nickname) || nickname.Any(char.IsWhiteSpace))
            throw new FormatException("o apelido é obrigatório e não pode conter espaços.");

        return new PeerOptions
        {
            ListenHost = endpoint.Host,
            ListenPort = endpoint.Port,
            Nickname = nickname,
            KnownPeers = ParsePeers(peers)
        };
    }

    private static List<PeerEndpoint> ParsePeers(string peers)
    {
        var endpoints = new List<PeerEndpoint>();

        foreach (var item in peers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!PeerEndpoint.TryParse(item, out var endpoint))
                throw new FormatException($"par conhecido inválido: '{item}'.");

            if (!endpoints.Contains(endpoint))
                endpoints.Add(endpoint);
        }

        return endpoints;
    }

    private static string Setting(Dictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out var value) ? value : string.Empty;
}

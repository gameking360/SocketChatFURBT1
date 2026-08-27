using System.Text;
using SocketChat.Peer;

namespace SocketChat.Ui;

public sealed class ChatCommands
{
    private delegate bool CommandHandler(string arguments);

    private const int MaxTextBytes = 32 * 1024;

    private readonly PeerNode _node;
    private readonly IChatOutput _output;
    private readonly Dictionary<string, CommandHandler> _commands;

    public ChatCommands(PeerNode node, IChatOutput output)
    {
        _node = node;
        _output = output;
        _commands = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["/list"] = ShowPeers,
            ["/msg"] = SendPrivate,
            ["/quit"] = _ => false
        };
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _output.Info("comandos: /list, /msg <apelido> <texto>, /quit");

        while (!ct.IsCancellationRequested)
        {
            var line = await ReadLineAsync(ct);
            if (line is null)
                return;

            if (line.Length == 0)
                continue;

            if (Encoding.UTF8.GetByteCount(line) > MaxTextBytes)
            {
                _output.Warning($"mensagem maior que {MaxTextBytes} bytes; reduza o texto.");
                continue;
            }

            if (!Execute(line))
                return;
        }
    }

    private bool Execute(string line)
    {
        if (line[0] != '/')
        {
            _node.Broadcast(line);
            return true;
        }

        var parts = line.Split(' ', 2);
        if (!_commands.TryGetValue(parts[0], out var command))
        {
            _output.Warning($"comando desconhecido: {parts[0]}");
            return true;
        }

        return command(parts.Length > 1 ? parts[1] : string.Empty);
    }

    private bool ShowPeers(string arguments)
    {
        var peers = _node.Peers;
        _output.Info($"participantes conhecidos ({peers.Count + 1}):");
        _output.Info($"  {_node.Nickname} (você) - {_node.Endpoint}");

        foreach (var peer in peers.OrderBy(peer => peer.Nickname))
            _output.Info($"  {peer.Nickname} - {peer.Endpoint}");

        return true;
    }

    private bool SendPrivate(string arguments)
    {
        var parts = arguments.Split(' ', 2);
        if (parts.Length < 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            _output.Warning("uso: /msg apelido texto");
            return true;
        }

        if (_node.SendPrivate(parts[0], parts[1]))
            _output.Private($"você -> {parts[0]}", parts[1]);
        else
            _output.Warning($"apelido não encontrado: {parts[0]}");

        return true;
    }

    private static async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var read = Task.Run(Console.ReadLine);
        var cancelled = Task.Delay(Timeout.Infinite, ct);
        return await Task.WhenAny(read, cancelled) == read ? await read : null;
    }
}

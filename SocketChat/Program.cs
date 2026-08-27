using System.Net.Sockets;
using SocketChat.Configuration;
using SocketChat.Peer;
using SocketChat.Ui;

var output = new ConsoleOutput();

PeerOptions options;
try
{
    options = PeerOptions.Parse(args);
}
catch (FormatException ex)
{
    output.Warning(ex.Message);
    output.Info(PeerOptions.Usage);
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var node = new PeerNode(options, output);
try
{
    node.Start(cts.Token);
    await new ChatCommands(node, output).RunAsync(cts.Token);
}
catch (SocketException ex)
{
    output.Warning($"erro de socket: {ex.SocketErrorCode} - {ex.Message}");
    return 1;
}
finally
{
    await node.StopAsync();
    output.System("sessão encerrada");
}

return 0;

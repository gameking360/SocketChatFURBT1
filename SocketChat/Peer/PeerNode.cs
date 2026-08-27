using System.Net;
using System.Net.Sockets;
using SocketChat.Configuration;
using SocketChat.Net;
using SocketChat.Protocol;
using SocketChat.Ui;

namespace SocketChat.Peer;

public sealed class PeerNode : IPeerEventListener
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromMilliseconds(300);

    private readonly PeerOptions _options;
    private readonly IChatOutput _output;
    private readonly PeerRegistry _registry;
    private CancellationTokenSource _cts = new();
    private Socket? _listener;
    private int _stopped;

    public PeerNode(PeerOptions options, IChatOutput output)
    {
        _options = options;
        _output = output;
        _registry = new PeerRegistry(options.Nickname);
        Endpoint = new PeerEndpoint("127.0.0.1", options.ListenPort);
    }

    public string Nickname => _options.Nickname;

    public PeerEndpoint Endpoint { get; }

    public IReadOnlyCollection<PeerConnection> Peers => _registry.Connections;

    public void Start(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Bind(new IPEndPoint(IPAddress.Any, _options.ListenPort));
        _listener.Listen(backlog: 32);

        _output.System($"escutando em {_listener.LocalEndPoint} como '{Nickname}'");

        _ = AcceptLoopAsync(_listener, _cts.Token);

        foreach (var peer in _options.KnownPeers)
            _ = ConnectAsync(peer, _cts.Token);
    }

    public void Broadcast(string text) => Deliver(Message.Chat(text), _registry.Connections);

    public bool SendPrivate(string nickname, string text)
    {
        var peer = _registry.Find(nickname);
        peer?.Send(Message.Private(text));
        return peer is not null;
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
            return;

        var peers = _registry.Connections;
        Deliver(Message.Bye("saiu do chat"), peers);

        if (peers.Count > 0)
            await Task.Delay(ShutdownGrace);

        foreach (var peer in peers)
            peer.Close("sessão encerrada");

        _cts.Cancel();
        _listener?.Dispose();
    }

    public async Task ConnectAsync(PeerEndpoint endpoint, CancellationToken ct)
    {
        if (endpoint == Endpoint || !_registry.TryBeginDial(endpoint))
            return;

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            using (var deadline = Deadline.After(ConnectTimeout, ct))
                await socket.ConnectAsync(endpoint.Host, endpoint.Port, deadline.Token);

            await Handshake.SendAsync(socket, Message.Hello(Nickname, _options.ListenPort), ct);
            var welcome = await Handshake.ReceiveAsync(socket, MessageType.Welcome, ct);

            if (Register(socket, endpoint, welcome.Field(0), outbound: true) is not null)
                Discover(welcome.Field(2));
        }
        catch (Exception ex)
        {
            socket.Dispose();
            _output.Warning($"não foi possível conectar em {endpoint}: {ex.Message}");
        }
        finally
        {
            _registry.EndDial(endpoint);
        }
    }

    public void OnMessage(PeerConnection peer, Message message)
    {
        switch (message.Type)
        {
            case MessageType.Chat:
                _output.Chat(peer.Nickname, message.Field(0));
                break;
            case MessageType.Private:
                _output.Private(peer.Nickname, message.Field(0));
                break;
            case MessageType.Peers:
                Discover(message.Field(0));
                break;
        }
    }

    public void OnDisconnected(PeerConnection peer, string reason)
    {
        if (!_registry.Remove(peer) || Volatile.Read(ref _stopped) == 1)
            return;

        _output.System($"{peer.Nickname} saiu ({reason})");
    }

    public void OnBackpressure(PeerConnection peer, int dropped) =>
        _output.Warning($"fila de envio para {peer.Nickname} está cheia; {dropped} mensagem(ns) descartada(s)");

    private async Task AcceptLoopAsync(Socket listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var socket = await listener.AcceptAsync(ct);
                _ = AcceptPeerAsync(socket, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                _output.Warning($"falha ao aceitar conexão: {ex.SocketErrorCode}");
            }
        }
    }

    private async Task AcceptPeerAsync(Socket socket, CancellationToken ct)
    {
        try
        {
            socket.NoDelay = true;

            var hello = await Handshake.ReceiveAsync(socket, MessageType.Hello, ct);
            var nickname = hello.Field(0);

            if (!int.TryParse(hello.Field(1), out var listenPort))
                throw new InvalidDataException("porta de escuta inválida.");

            var endpoint = PeerEndpoint.FromSocket(socket.RemoteEndPoint, listenPort);
            await EnsureNicknameIsFreeAsync(nickname, endpoint, socket, ct);

            var welcome = Message.Welcome(Nickname, _options.ListenPort, _registry.Endpoints);
            await Handshake.SendAsync(socket, welcome, ct);

            if (Register(socket, endpoint, nickname, outbound: false) is not null)
                Announce(endpoint);
        }
        catch (Exception ex)
        {
            socket.Dispose();
            _output.Warning($"conexão de entrada recusada: {ex.Message}");
        }
    }

    private async Task EnsureNicknameIsFreeAsync(string nickname, PeerEndpoint endpoint, Socket socket, CancellationToken ct)
    {
        var taken = string.Equals(nickname, Nickname, StringComparison.OrdinalIgnoreCase)
                    || _registry.IsNicknameTaken(nickname, endpoint);

        if (!taken)
            return;

        var reason = $"o apelido '{nickname}' já está em uso na malha.";
        await Handshake.SendAsync(socket, Message.Bye(reason), ct);
        throw new InvalidDataException(reason);
    }

    private PeerConnection? Register(Socket socket, PeerEndpoint endpoint, string nickname, bool outbound)
    {
        var connection = new PeerConnection(socket, endpoint, nickname, outbound, this, _cts.Token);

        var registration = _registry.Register(connection);
        if (registration is PeerRegistration.Rejected)
        {
            connection.Close("conexão duplicada");
            return null;
        }

        connection.Start();

        if (registration is PeerRegistration.Added)
            _output.System($"{nickname} entrou ({endpoint})");

        return connection;
    }

    private void Announce(PeerEndpoint joined) =>
        Deliver(Message.Peers([joined]), _registry.Connections.Where(peer => peer.Endpoint != joined));

    private void Discover(string csv)
    {
        foreach (var endpoint in PeerEndpoint.ParseList(csv))
        {
            if (endpoint != Endpoint && !_registry.Knows(endpoint))
                _ = ConnectAsync(endpoint, _cts.Token);
        }
    }

    private static void Deliver(Message message, IEnumerable<PeerConnection> peers)
    {
        foreach (var peer in peers)
            peer.Send(message);
    }
}

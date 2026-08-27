using System.Net.Sockets;
using SocketChat.Net;
using SocketChat.Protocol;

namespace SocketChat.Peer;

public sealed class PeerConnection
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(45);
    private static readonly byte[] Heartbeat = Message.Ping().ToBytes();
    private const int OutboxCapacity = 200;

    private readonly Socket _socket;
    private readonly IPeerEventListener _listener;
    private readonly PeerOutbox _outbox = new(OutboxCapacity);
    private readonly CancellationTokenSource _cts;
    private int _closed;
    private bool _congested;

    public PeerConnection(
        Socket socket,
        PeerEndpoint endpoint,
        string nickname,
        bool outbound,
        IPeerEventListener listener,
        CancellationToken ct)
    {
        _socket = socket;
        _listener = listener;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Endpoint = endpoint;
        Nickname = nickname;
        Outbound = outbound;
    }

    public PeerEndpoint Endpoint { get; }

    public string Nickname { get; }

    public bool Outbound { get; }

    public void Start()
    {
        _ = ReceiveLoopAsync();
        _ = SendLoopAsync();
    }

    public void Send(Message message)
    {
        if (_outbox.TryEnqueue(message.ToBytes()))
        {
            _congested = false;
            return;
        }

        if (_congested)
            return;

        _congested = true;
        _listener.OnBackpressure(this, _outbox.Dropped);
    }

    public void Close(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
            return;

        _cts.Cancel();

        try { _socket.Shutdown(SocketShutdown.Both); }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }

        _socket.Dispose();
        _listener.OnDisconnected(this, reason);
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                using var deadline = Deadline.After(IdleTimeout, _cts.Token);
                var frame = await Frames.ReadAsync(_socket, deadline.Token);

                if (frame is null)
                {
                    Close("conexão encerrada");
                    return;
                }

                var message = Message.Parse(frame);
                if (message is null || message.Type is MessageType.Ping)
                    continue;

                if (message.Type is MessageType.Bye)
                {
                    Close(message.Field(0));
                    return;
                }

                _listener.OnMessage(this, message);
            }
        }
        catch (OperationCanceledException)
        {
            Close(_cts.IsCancellationRequested ? "sessão encerrada" : "par sem resposta");
        }
        catch (SocketException)
        {
            Close("conexão perdida");
        }
        catch (Exception ex)
        {
            Close(ex.Message);
        }
    }

    private async Task SendLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var payload = await _outbox.DequeueAsync(HeartbeatInterval, _cts.Token) ?? Heartbeat;
                using var deadline = Deadline.After(SendTimeout, _cts.Token);
                await Frames.WriteAsync(_socket, payload, deadline.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Close(_cts.IsCancellationRequested ? "sessão encerrada" : "envio expirou");
        }
        catch (SocketException)
        {
            Close("conexão perdida");
        }
        catch (Exception ex)
        {
            Close(ex.Message);
        }
    }
}

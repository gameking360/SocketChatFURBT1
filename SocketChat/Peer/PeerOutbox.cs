namespace SocketChat.Peer;

public sealed class PeerOutbox
{
    private readonly Queue<byte[]> _queue = new();
    private readonly SemaphoreSlim _pending = new(0);
    private readonly object _gate = new();
    private readonly int _capacity;

    public PeerOutbox(int capacity) => _capacity = capacity;

    public int Dropped { get; private set; }

    public bool TryEnqueue(byte[] payload)
    {
        lock (_gate)
        {
            if (_queue.Count >= _capacity)
            {
                Dropped++;
                return false;
            }
            _queue.Enqueue(payload);
        }

        _pending.Release();
        return true;
    }

    public async Task<byte[]?> DequeueAsync(TimeSpan timeout, CancellationToken ct)
    {
        if (!await _pending.WaitAsync(timeout, ct))
            return null;

        lock (_gate)
            return _queue.Dequeue();
    }
}

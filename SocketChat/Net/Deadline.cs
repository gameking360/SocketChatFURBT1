namespace SocketChat.Net;

public static class Deadline
{
    public static CancellationTokenSource After(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        return deadline;
    }
}

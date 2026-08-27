using System.Net.Sockets;
using SocketChat.Net;

namespace SocketChat.Protocol;

public static class Handshake
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static async Task SendAsync(Socket socket, Message message, CancellationToken ct)
    {
        using var deadline = Deadline.After(Timeout, ct);
        await Frames.WriteAsync(socket, message.ToBytes(), deadline.Token);
    }

    public static async Task<Message> ReceiveAsync(Socket socket, MessageType expected, CancellationToken ct)
    {
        using var deadline = Deadline.After(Timeout, ct);
        var frame = await Frames.ReadAsync(socket, deadline.Token);
        var message = frame is null ? null : Message.Parse(frame);

        if (message is { Type: MessageType.Bye })
            throw new InvalidDataException(message.Field(0));

        if (message is null || message.Type != expected)
            throw new InvalidDataException($"handshake inválido: esperava {expected}.");

        return message;
    }
}

using System.Buffers.Binary;
using System.Net.Sockets;

namespace SocketChat.Net;

public static class Frames
{
    public const int MaxFrameSize = 64 * 1024;

    public static async Task WriteAsync(Socket socket, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        if (payload.Length > MaxFrameSize)
            throw new ArgumentException($"Payload de {payload.Length} bytes excede o limite.");

        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);

        await SendAllAsync(socket, header, ct);
        await SendAllAsync(socket, payload, ct);
    }

    public static async Task<byte[]?> ReadAsync(Socket socket, CancellationToken ct = default)
    {
        var header = new byte[4];
        if (!await ReadExactlyAsync(socket, header, ct))
            return null;

        var size = BinaryPrimitives.ReadInt32BigEndian(header);
        if (size < 0 || size > MaxFrameSize)
            throw new InvalidDataException($"Tamanho de quadro inválido: {size}");

        if (size == 0)
            return [];

        var payload = new byte[size];
        if (!await ReadExactlyAsync(socket, payload, ct))
            throw new EndOfStreamException("Conexão encerrada no meio de um quadro.");

        return payload;
    }

    private static async Task SendAllAsync(Socket socket, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var sent = 0;
        while (sent < data.Length)
        {
            var n = await socket.SendAsync(data[sent..], SocketFlags.None, ct);
            if (n == 0)
                throw new SocketException((int)SocketError.ConnectionReset);
            sent += n;
        }
    }

    private static async Task<bool> ReadExactlyAsync(Socket socket, Memory<byte> destination, CancellationToken ct)
    {
        var read = 0;
        while (read < destination.Length)
        {
            var n = await socket.ReceiveAsync(destination[read..], SocketFlags.None, ct);
            if (n == 0)
            {
                if (read == 0)
                    return false;

                throw new EndOfStreamException($"Faltam {destination.Length - read} bytes do quadro.");
            }
            read += n;
        }
        return true;
    }
}

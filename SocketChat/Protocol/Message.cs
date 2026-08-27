using System.Text;
using SocketChat.Net;

namespace SocketChat.Protocol;

public enum MessageType
{
    Hello,
    Welcome,
    Peers,
    Chat,
    Private,
    Bye,
    Ping
}

public sealed class Message
{
    private const char Separator = '\t';

    private readonly string[] _fields;

    private Message(MessageType type, params string[] fields)
    {
        Type = type;
        _fields = fields;
    }

    public MessageType Type { get; }

    public string Field(int index) => index < _fields.Length ? _fields[index] : string.Empty;

    public static Message Hello(string nickname, int listenPort) =>
        new(MessageType.Hello, nickname, listenPort.ToString());

    public static Message Welcome(string nickname, int listenPort, IEnumerable<PeerEndpoint> peers) =>
        new(MessageType.Welcome, nickname, listenPort.ToString(), PeerEndpoint.ToCsv(peers));

    public static Message Peers(IEnumerable<PeerEndpoint> peers) =>
        new(MessageType.Peers, PeerEndpoint.ToCsv(peers));

    public static Message Chat(string text) => new(MessageType.Chat, text);

    public static Message Private(string text) => new(MessageType.Private, text);

    public static Message Bye(string reason) => new(MessageType.Bye, reason);

    public static Message Ping() => new(MessageType.Ping);

    public byte[] ToBytes() =>
        Encoding.UTF8.GetBytes(string.Join(Separator, [Type.ToString(), .._fields]));

    public static Message? Parse(byte[] frame)
    {
        var text = Encoding.UTF8.GetString(frame);
        var head = text.Split(Separator, 2)[0];

        if (!Enum.TryParse<MessageType>(head, ignoreCase: true, out var type) || !Enum.IsDefined(type))
            return null;

        // Limitar a contagem faz o último campo absorver tabs do texto livre.
        var parts = text.Split(Separator, FieldCount(type) + 1);
        return new Message(type, parts[1..]);
    }

    private static int FieldCount(MessageType type) => type switch
    {
        MessageType.Hello => 2,
        MessageType.Welcome => 3,
        MessageType.Peers or MessageType.Chat or MessageType.Private or MessageType.Bye => 1,
        _ => 0
    };
}

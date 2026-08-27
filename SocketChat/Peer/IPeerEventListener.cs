using SocketChat.Protocol;

namespace SocketChat.Peer;

public interface IPeerEventListener
{
    void OnMessage(PeerConnection peer, Message message);

    void OnDisconnected(PeerConnection peer, string reason);

    void OnBackpressure(PeerConnection peer, int dropped);
}

namespace SocketChat.Ui;

public interface IChatOutput
{
    void Chat(string author, string text);

    void Private(string author, string text);

    void System(string text);

    void Warning(string text);

    void Info(string text);
}

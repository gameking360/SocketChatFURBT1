using System.Text;

namespace SocketChat.Ui;

public sealed class ConsoleOutput : IChatOutput
{
    private readonly object _gate = new();

    public ConsoleOutput() => Console.OutputEncoding = Encoding.UTF8;

    public void Chat(string author, string text) => Write(ConsoleColor.Gray, $"{Stamp()} {author}: {text}");

    public void Private(string author, string text) => Write(ConsoleColor.Cyan, $"{Stamp()} [privado] {author}: {text}");

    public void System(string text) => Write(ConsoleColor.DarkGray, $"{Stamp()} * {text}");

    public void Warning(string text) => Write(ConsoleColor.Yellow, $"{Stamp()} ! {text}");

    public void Info(string text) => Write(ConsoleColor.White, text);

    private static string Stamp() => DateTime.Now.ToString("HH:mm:ss");

    private void Write(ConsoleColor color, string line)
    {
        lock (_gate)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ForegroundColor = previous;
        }
    }
}

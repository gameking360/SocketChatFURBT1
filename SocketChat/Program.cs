

using System.Net.Sockets;
using SocketChat;

if (args.Length == 0)
{
    PrintTutorial();
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};



try
{
    if(args.Length < 2)
    {
        PrintTutorial();
        return 1;
    }
    int    porta = ReadInt(args, 0, 9000);
    string apelido = ReadText(args, 1, "conexao");
    string[] names = args.Skip(2).ToArray();
    
    Connection conexao = new Connection(apelido, porta , "127.0.0.1");
    await conexao.CreateConnection(cts.Token, names) ;

    await ChatSession.RunAsync(conexao, apelido, cts.Token) ;
    return 0;

}catch(OperationCanceledException ex)
{
    Console.WriteLine(ex.Message);
    return 0;
}
catch (SocketException ex)
{
    Console.Error.WriteLine($"Socket error: {ex.SocketErrorCode} - {ex.Message}");
    return 1;
}





static int ReadInt(string[] args, int index, int fallback) =>
    args.Length > index && int.TryParse(args[index], out var value) ? value : fallback;

static string ReadText(string[] args, int index, string fallback) =>
    args.Length > index && !string.IsNullOrWhiteSpace(args[index]) ? args[index] : fallback;


static void PrintTutorial() => Console.WriteLine("Tutoral");
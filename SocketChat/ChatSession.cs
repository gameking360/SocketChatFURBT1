using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SocketChat.DTO;

namespace SocketChat
{
    internal class ChatSession
    {
        static ConcurrentDictionary<string, Conexao> Conexoes = new ConcurrentDictionary<string, Conexao>();
        public static Conexao GetConnection(string name)
        {
            if (Conexoes.TryGetValue(name, out var connection))
            {
                return connection;
            }
            throw new InvalidOperationException();
        }


        public static bool AddConnection(string name, Conexao connection) => Conexoes.TryAdd(name, connection);


        public static async Task RunAsync(Connection conexao, string name, CancellationToken ct)
        {
  

            while (!ct.IsCancellationRequested)
            {




                var list = Task.Run(async () =>
                {
                        var connectedClient = await conexao.Listener.AcceptAsync(ct);
                        conexao.Senders.Add(connectedClient);
                    _ =  ReceiveLoopAsync(connectedClient, ct);

                });

                var sender = Task.Run(async () => await SendLoopAsync(conexao, name, ct));


                Task.WaitAny(list, sender);
            }

            try
            {
                conexao.Listener.Shutdown(SocketShutdown.Both);
                conexao.Senders.ForEach(f => f.Shutdown(SocketShutdown.Both));
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            Console.WriteLine("Session closed.");
        }

        private static async Task ReceiveLoopAsync(Socket socket, CancellationToken session)
        {
            try
            {


                while (!session.IsCancellationRequested)
                {

                    var frame = await Frames.ReadAsync(socket, session);
                    if (frame is null)
                    {
                        continue;
                    }

                    string message = Encoding.UTF8.GetString(frame);


                    Console.WriteLine(Encoding.UTF8.GetString(frame));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[receive failed: {ex.GetType().Name} - {ex.Message}]");
            }

        }

        private static async Task SendLoopAsync(Connection conexao, string name, CancellationToken session)
        {
            try
            {
                while (!session.IsCancellationRequested)
                {
                    var line = await Task.Run(Console.ReadLine);

                    if (line is null)
                        return;

                    if (line.Length == 0)
                        continue;

                    var payload = Encoding.UTF8.GetBytes($"{name}: {line}");
                    foreach(var sender in conexao.Senders)
                    await Frames.WriteAsync(sender, payload, session);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[send failed: {ex.GetType().Name} - {ex.Message}]");
            }

        }


        //private static async Task EspalharConexoes(Conexao conexao, Socket sender, CancellationToken cancellationToken)
        //{


        //    var conexoes = Conexoes.Select(e => e.Value).ToArray();
        //    var package = JsonSerializer.SerializeToUtf8Bytes(conexoes,)
        //    await Frames.WriteAsync(sender,);
        //}
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SocketChat
{
    public class Connection
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);


        public int Port { get; private set; }

        public string Name { get; private set; }
        public string Host { get; private set; }

        public List<Socket> Senders { get; private set; }
        public Socket Listener { get; private set; }

        public Connection(string name, int port, string host)
        {
            this.Name = name;
            this.Port = port;
            this.Host = host;
            this.Senders = new List<Socket>();




            this.Listener = new Socket(
                addressFamily: AddressFamily.InterNetwork,
                socketType: SocketType.Stream,
                protocolType: ProtocolType.Tcp);
        }



        public async Task CreateConnection(CancellationToken ct, params string[] names)
        {
            await this.ListenAsync(ct);
            await this.ConnectAsync(ct, names);
        }


        public async Task ListenAsync(CancellationToken ct)
        {
            this.Listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            this.Listener.Bind(new IPEndPoint(IPAddress.Any, this.Port));
            this.Listener.Listen(99);

            Console.WriteLine($"Listening on {this.Listener.LocalEndPoint}. Waiting for a peer...");


        }

        public async Task ConnectAsync(CancellationToken ct, string[] names)
        {
            foreach (var name in names)
            {

                Socket sender = new Socket(
                addressFamily: AddressFamily.InterNetwork,
                socketType: SocketType.Stream,
                protocolType: ProtocolType.Tcp)
                { NoDelay = true };

                try
                {
                    Console.WriteLine(sender.RemoteEndPoint);

                    //var conexao = ChatSession.GetConnection(name);
                    Console.WriteLine($"Conectando em {this.Host}:{name}");

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(ConnectTimeout);
                    await sender.ConnectAsync(this.Host, int.Parse(name), timeout.Token);

                    String id = "conexao-" + int.Parse(name);
                    ChatSession.AddConnection(id, new DTO.Conexao() { Host = this.Host, Name = id, Porta = this.Port });

                    this.Senders.Add(sender);


                }
                catch (Exception ex)
                {
                    sender.Dispose();
                    Console.WriteLine("Error " + ex.Message);
                    throw;
                }
            }

        }





    }
}

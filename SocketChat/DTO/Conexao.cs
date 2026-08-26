using System;
using System.Collections.Generic;
using System.Text;

namespace SocketChat.DTO
{
    public class Conexao
    {
        public required string Host {  get; set; }
        public required int Porta { get; set; }
        public required string Name { get; set; }
    }
}

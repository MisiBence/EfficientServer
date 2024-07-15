using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;

namespace EfficientServer
{
    internal class Server
    {
        static void Main(string[] args)
        {            
            Communication communication;

            Console.WriteLine("Server is running...");

            communication = new Communication(12345);
            communication.Listen();
        }
    }
}

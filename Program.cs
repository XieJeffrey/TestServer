using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestServer.ServerCore;
using System.Net.Sockets;
using System.Net;

namespace TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            NetworkManager networkManager = new NetworkManager(IPAddress.Parse("127.0.0.1"),9900);
            networkManager.Start();
            while (true)
            {
                if (Console.ReadKey() != null)
                {
                    break;
                }
            }         
        }
    }
}

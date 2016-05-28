using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace dsocks2
{
    class Program
    {
        static void Pipe(Socket s1, Socket s2)
        {
            byte[] buf = new byte[0x10000];

            while (true)
            {
                try
                {
                    int len = s1.Receive(buf);
                    if(len == 0)
                        break;
                    s2.Send(buf, len, SocketFlags.None);
                }
                catch
                {
                    break;
                }
            }

            s1.Close();
            s2.Close();
            Console.WriteLine("Done");
        }

        static bool ParseArgs(string[] args, ref dChain chain)
        {
            if (args.Length < 1)
                return false;

            for (var i = 0; i < args.Length; i++)
                chain.Add(args[i]);

            return true;
        }

        static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("dsocks2 [user:pass@]proxy.com[:1080] [proxy2 proxy3 ...]");
        }

        static void Main(string[] args)
        {
            var listenAddr = "0.0.0.0";
            var listenPort = 1080;
            var socketTimeout = 5000;

            var chain = new dChain(socketTimeout);

            if (!ParseArgs(args, ref chain))
            {
                PrintHelp();
                return;
            }


            Console.WriteLine("Starting TCP listener..");
            TcpListener listener = new TcpListener(IPAddress.Parse(listenAddr), listenPort);
            listener.Start(100);

            while (true)
            {
                Console.WriteLine("Accepting connection");
                var client = listener.AcceptSocket();
                var clientSocks = new dSocks(client);

                new Thread(() =>
                {
                    if (clientSocks.ServerInit() && clientSocks.ParseRequest() && clientSocks.ServerReply(0))
                    {
                        Console.WriteLine("Target {0} {1}", clientSocks.domain, clientSocks.port);

                        Console.WriteLine("Connecting...");
                        var server = chain.Connect();

                        if (!server.Connected)
                        {
                            Console.WriteLine("Couldn't connect to server");
                            client.Close();
                        }
                        else
                        {
                            Console.WriteLine("OK");
                            Task.Factory.StartNew(() =>
                            {
                                chain.Push(server);
                                chain.PushRaw(server, clientSocks.lastRequest);
                                Pipe(client, server);
                            });
                            Task.Factory.StartNew(() =>
                            {
                                chain.Pull(server);
                                Pipe(server, client);
                            });
                        }
                    }
                    else
                    {
                        client.Close();
                    }
                }).Start();
            }
        }
    }
}

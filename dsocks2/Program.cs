using System;
using System.Collections.Generic;
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

        static bool ParseArgs(string[] args, out List<Socks5> chain)
        {
            chain = new List<Socks5>();
            if (args.Length < 1)
                return false;

            for (var i = 0; i < args.Length; i++)
            {
                var sock = new Socks5(args[i]);
                if (sock == null)
                    return false;

                chain.Add(sock);
            }

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

            List<Socks5> chain = null;

            if (!ParseArgs(args, out chain))
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
                var clientSocks = new Socks5(client);

                new Thread(() =>
                {
                    if (clientSocks.ServerInit() && clientSocks.ParseRequest())
                    {
                        Console.WriteLine("Target {0} {1}", clientSocks.domain, clientSocks.port);

                        Console.WriteLine("Connecting...");
                        var server = chain[0].Connect();
                        var status = 0;
                        var fail = true;

                        if (!server.Connected)
                        {
                            Console.WriteLine("Jump [{0}] at {1} is unreachable", 1, chain[0].xHost);
                            status = 4;
                        }
                        else
                        {
                            for (var i = 0; i < chain.Count; i++)
                            {
                                var serverSocks = new Socks5(chain[i], server);
                                if (!serverSocks.ClientInit(server)) Console.WriteLine("Jump [{0}] at {1} refused socks connection", i + 1, serverSocks.xHost);
                                else
                                {
                                    var last = (i == chain.Count - 1);
                                    var host = last ? clientSocks.domain : chain[i + 1].xHost;
                                    var port = last ? clientSocks.port : chain[i + 1].xPort;
                                    
                                    fail = !serverSocks.ClientConnect(host, port);
                                    status = serverSocks.status;
                                    
                                    if (fail)
                                    {
                                        Console.WriteLine("Jump [{0}] at {1} is unreachable", i + 2, host);
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (!clientSocks.ServerReply(status))
                        {
                            Console.WriteLine("Client went away");
                            fail = true;
                        }

                        if (fail)
                        {
                            client.Close();
                            server.Close();
                        }
                        else
                        {
                            Console.WriteLine("OK");
                            fail = false;
                            Task.Factory.StartNew(() => Pipe(client, server));
                            Task.Factory.StartNew(() => Pipe(server, client));
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

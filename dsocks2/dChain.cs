using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace dsocks2
{
    // experimental socks chain for faster multi-jump connections
    // prepares & dumps the requests without waiting for every reply
    // only supports connect + domain for now
    class dChain
    {
        private const int ver = 5;
        private byte[] writeBuf;
        private byte[] readBuf;

        private bool firstJump = true;
        private string firstHost;
        private int firstPort;

        private int socketTimeout;

        public dChain(int timeout)
        {
            socketTimeout = timeout;
        }

        // todo: check for invalid strings
        // [user:pass@]socks.addr[:port]
        public bool Add(string con)
        {
            var match = new Regex("^(?:(.*?):(.*?)@)?(.*?)(?::(\\d+))?$").Match(con).Groups;

            var user = (match[1].Success) ? match[1].Value : null;
            var pass = (match[2].Success) ? match[2].Value : null;
            var host = match[3].Value;
            var port = (match[4].Success) ? int.Parse(match[4].Value) : 1080;

            var write = new List<byte>();
            var read = new List<byte>();

            if (writeBuf != null) write.AddRange(writeBuf);
            if (readBuf != null) read.AddRange(readBuf);

            if (firstJump)
            {
                firstHost = host;
                firstPort = port;
                firstJump = false;
            }
            else
            {
                write.AddRange(new byte[] { ver, 1, 0, 3, (byte)host.Length });
                write.AddRange(Encoding.ASCII.GetBytes(host));
                write.AddRange(new byte[] { (byte)(port >> 8), (byte)(port & 255) });
            }

            write.AddRange(new byte[] { ver, 1 });
            read.Add(ver);

            if (user != null && pass != null)
            {
                // todo: should probably check if len <= 255
                write.AddRange(new byte[] { 2, 1, (byte)user.Length });
                write.AddRange(Encoding.ASCII.GetBytes(user));
                write.AddRange(new byte[] { (byte)pass.Length });
                write.AddRange(Encoding.ASCII.GetBytes(pass));

                read.AddRange(new byte[] { 2, ver });
            }
            else
                write.Add(0);

            read.AddRange(new byte[] { 0, ver, 0, 0, 1, 0, 0, 0, 0, 0, 0 });

            writeBuf = write.ToArray();
            readBuf = read.ToArray();

            return true;
        }

        public Socket Connect()
        {
            if (firstHost == null)
                return null;

            var socket = new TcpClient(firstHost, firstPort).Client;
            socket.ReceiveTimeout = socketTimeout;
            socket.SendTimeout = socketTimeout;

            return socket;
        }

        public bool PushRaw(Socket sock, byte[] msg)
        {
            if (!sock.Connected)
                return false;

            try
            {
                sock.Send(msg);
            }
            catch { return false; }

            return true;
        }

        public bool Push(Socket sock)
        {
            return PushRaw(sock, writeBuf);
        }

        public bool Pull(Socket sock)
        {
            if (!sock.Connected)
                return false;

            try
            {
                var buf = new byte[readBuf.Length];
                var read = 0;
                while (read != readBuf.Length)
                {
                    var len = sock.Receive(buf, readBuf.Length, SocketFlags.None);
                    if (0 == len) break;
                    read += len;
                }
            }
            catch { return false; }

            return true;
        }
    }
}

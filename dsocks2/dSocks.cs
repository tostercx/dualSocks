using System;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace dsocks2
{
    // crappy socks5 wrapper for a socket
    class dSocks
    {
        private Socket socket;
        private const int ver = 5;

        // proxy settings
        public string xHost { private set; get; }
        public int xPort { private set; get; }
        public string xUser { private set; get; }
        public string xPass { private set; get; }

        // parsed stuff
        public string domain { private set; get; }
        public int port { private set; get; }
        public int status { private set; get; }

        // client setup [user:pass@]socks.addr[:port]
        public dSocks(string con)
        {
            var match = new Regex("^(?:(.*?):(.*?)@)?(.*?)(?::(\\d+))?$").Match(con).Groups;

            xUser = (match[1].Success) ? match[1].Value : null;
            xPass = (match[2].Success) ? match[2].Value : null;
            xHost = match[3].Value;
            xPort = (match[4].Success) ? int.Parse(match[4].Value) : 1080;
        }

        // server setup
        public dSocks(Socket sock)
        {
            socket = sock;
        }

        // evil clones?
        public dSocks(dSocks mommy, Socket daddy)
        {
            xUser = mommy.xUser;
            xPass = mommy.xPass;
            xHost = mommy.xHost;
            xPort = mommy.xPort;
            socket = daddy;
        }

        public Socket Connect()
        {
            if (xHost == null)
                return null;

            return new TcpClient(xHost, xPort).Client;
        }

        private byte[] Read(int len)
        {
            var buf = new byte[len];
            try
            {
                socket.Receive(buf, len, SocketFlags.None);
            }
            catch
            {
                return null;
            }
            return buf;
        }

        private string ReadDNS()
        {
            try
            {
                var len = Read(1);
                var buf = Read(len[0]);
                return Encoding.ASCII.GetString(buf);
            }
            catch { }

            return "";
        }

        private int ReadPort()
        {
            var buf = Read(2);
            return (buf == null) ? -1 : (int)((buf[0] << 8) | buf[1]);
        }

        private void Write(byte[] buf)
        {
            try
            {
                socket.Send(buf);
            }
            catch { }
        }

        public bool ServerInit()
        {
            var buf = Read(3);
            Write(new byte[] { ver, 0 });

            return true;
        }

        private bool ParseInitReply()
        {
            var buf = Read(2);
            if (buf != null && buf[1] == 0)
                return true;

            return false;
        }

        public bool ClientInit(Socket sock)
        {
            if (xUser != null && xPass != null)
            {
                Write(new byte[] { ver, 1, 2 });
                var buf = Read(2);

                if (buf == null || buf[0] != 5 || buf[1] != 2)
                    return false;

                Write(new byte[] { 1, (byte)xUser.Length });
                Write(Encoding.ASCII.GetBytes(xUser));
                Write(new byte[] { (byte)xPass.Length });
                Write(Encoding.ASCII.GetBytes(xPass));
            }
            else
                Write(new byte[] { ver, 1, 0 });

            return ParseInitReply();
        }

        public bool ClientConnect(string domain, int port)
        {
            Write(new byte[] { ver, 1, 0, 3, (byte)domain.Length });
            Write(Encoding.ASCII.GetBytes(domain));
            Write(new byte[] { (byte)(port >> 8), (byte)(port & 255) });

            if (!ParseRequest() || status != 0)
                return false;

            return true;
        }

        public bool ServerReply(int status)
        {
            try
            {
                Write(new byte[] { ver, (byte)status, 0, 1, 0, 0, 0, 0, 0, 0 });
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool ParseRequest()
        {
            var buf = Read(4);
            if (buf == null || buf[0] != ver)
                return false;
            
            status = buf[1];

            switch (buf[3])
            {
                case 1:
                    // dummy
                    Read(4);
                    break;

                case 3:
                    domain = ReadDNS();
                    if (domain == "")
                        return false;
                    break;

                case 4:
                    // dummy
                    Read(16);
                    break;
            }

            port = ReadPort();
            if (port == -1)
                return false;

            return true;
        }
    }
}

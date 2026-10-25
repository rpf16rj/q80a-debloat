using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// sdbd loopback push — writes a file on the TV as the sdbd context (uid 5001).
// Usage: SdbPush.exe <localfile> <remotepath>
internal static class SdbPush
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

    private static NetworkStream ns;
    private static byte[] rd = new byte[65536];
    private static int rdbuf = 0;

    private static bool Pump(out string cmd, out uint a0, out uint a1, out byte[] body, int timeoutMs)
    {
        cmd = null; a0 = a1 = 0; body = null;
        var end = Environment.TickCount + timeoutMs;
        while (Environment.TickCount < end)
        {
            if (rdbuf < 24 && ns.DataAvailable)
            {
                int r = ns.Read(rd, rdbuf, rd.Length - rdbuf);
                if (r <= 0) return false;
                rdbuf += r;
            }
            if (rdbuf >= 24)
            {
                int rl = BitConverter.ToInt32(rd, 12);
                if (rdbuf >= 24 + rl)
                {
                    cmd = Encoding.ASCII.GetString(rd, 0, 4);
                    a0 = BitConverter.ToUInt32(rd, 4); a1 = BitConverter.ToUInt32(rd, 8);
                    body = new byte[rl];
                    Buffer.BlockCopy(rd, 24, body, 0, rl);
                    Buffer.BlockCopy(rd, 24 + rl, rd, 0, rdbuf - 24 - rl);
                    rdbuf -= 24 + rl;
                    return true;
                }
                else if (ns.DataAvailable)
                {
                    int r = ns.Read(rd, rdbuf, rd.Length - rdbuf);
                    if (r <= 0) return false;
                    rdbuf += r;
                }
            }
            Thread.Sleep(20);
        }
        return false;
    }

    private static void Wrte(uint local, uint remote, byte[] data)
    {
        var p = Packet("WRTE", local, remote, data); ns.Write(p, 0, p.Length);
        // wait for OKAY
        string c; uint a0, a1; byte[] b;
        while (Pump(out c, out a0, out a1, out b, 10000))
        {
            if (c == "OKAY" && a1 == local) return;
            if (c == "CLSE") throw new Exception("CLSE on write");
            if (c == "WRTE") { var ack = Packet("OKAY", a1, a0, new byte[0]); ns.Write(ack, 0, ack.Length); }
        }
        throw new Exception("no OKAY for WRTE");
    }

    private static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("usage: SdbPush local remote"); return 2; }
        byte[] data = File.ReadAllBytes(args[0]);
        var tcp = new TcpClient();
        tcp.Connect("127.0.0.1", 26101);
        ns = tcp.GetStream();
        var cnxn = Packet("CNXN", 0x01000000, 4096, Encoding.ASCII.GetBytes("host::\0"));
        ns.Write(cnxn, 0, cnxn.Length);
        string c; uint a0, a1; byte[] b;
        if (!Pump(out c, out a0, out a1, out b, 10000) || c != "CNXN") { Console.WriteLine("no CNXN"); return 1; }
        Console.WriteLine("cnxn ok");

        var svc = Encoding.ASCII.GetBytes("sync:\0");
        var op = Packet("OPEN", 1, 0, svc); ns.Write(op, 0, op.Length);
        uint remote = 0;
        if (!Pump(out c, out a0, out a1, out b, 10000) || c != "OKAY") { Console.WriteLine("open failed " + c); return 1; }
        remote = a0;
        Console.WriteLine("sync open remote=" + remote);

        // SEND <len><path>,<mode>
        var spec = Encoding.ASCII.GetBytes(args[1] + ",33204");
        var send = new byte[4 + 4 + spec.Length];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("SEND"), 0, send, 0, 4);
        Buffer.BlockCopy(U32((uint)spec.Length), 0, send, 4, 4);
        Buffer.BlockCopy(spec, 0, send, 8, spec.Length);
        Wrte(1, remote, send);

        int step = 3000;
        for (int i = 0; i < data.Length; i += step)
        {
            int n = Math.Min(step, data.Length - i);
            var rec = new byte[8 + n];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("DATA"), 0, rec, 0, 4);
            Buffer.BlockCopy(U32((uint)n), 0, rec, 4, 4);
            Buffer.BlockCopy(data, i, rec, 8, n);
            Wrte(1, remote, rec);
        }
        var done = new byte[8];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("DONE"), 0, done, 0, 4);
        Buffer.BlockCopy(U32((uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds)), 0, done, 4, 4);
        Wrte(1, remote, done);

        // read response record
        while (Pump(out c, out a0, out a1, out b, 10000))
        {
            if (c == "WRTE")
            {
                var ack = Packet("OKAY", a1, a0, new byte[0]); ns.Write(ack, 0, ack.Length);
                if (b.Length >= 8)
                {
                    string kind = Encoding.ASCII.GetString(b, 0, 4);
                    string msg = Encoding.ASCII.GetString(b, 8, b.Length - 8).TrimEnd('\0');
                    Console.WriteLine("resp " + kind + " " + msg);
                    if (kind == "OKAY" || kind == "FAIL") break;
                }
            }
            else if (c == "CLSE") break;
        }
        var quit = new byte[8];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("QUIT"), 0, quit, 0, 4);
        try { var qp = Packet("WRTE", 1, remote, quit); ns.Write(qp, 0, qp.Length); } catch { }
        return 0;
    }
}

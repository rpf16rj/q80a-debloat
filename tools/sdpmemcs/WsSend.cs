using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Minimal WS client: connects to ws://127.0.0.1:8081 and sends one JSON frame.
// Used to trigger TizenBrew service events (e.g. StartService=6) on-TV.
internal static class WsSend
{
    private static int Main(string[] args)
    {
        string json = args.Length > 0 ? string.Join(" ", args)
            : "{\"type\":6,\"payload\":{\"fullName\":\"local/wled-ambi\",\"serviceFile\":\"service.js\"}}";
        if (json.StartsWith("file:")) json = File.ReadAllText(json.Substring(5)).Trim();

        var tcp = new TcpClient();
        tcp.Connect("127.0.0.1", 8081);
        var ns = tcp.GetStream();

        var key = Convert.ToBase64String(Encoding.ASCII.GetBytes("0123456789abcdef"));
        var hs = "GET / HTTP/1.1\r\nHost: 127.0.0.1:8081\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: " + key + "\r\nSec-WebSocket-Version: 13\r\n\r\n";
        var hsb = Encoding.ASCII.GetBytes(hs);
        ns.Write(hsb, 0, hsb.Length);

        var rd = new byte[8192];
        var sw = new StringBuilder();
        var deadline = Environment.TickCount + 8000;
        while (!sw.ToString().Contains("\r\n\r\n") && Environment.TickCount < deadline)
        {
            if (ns.DataAvailable)
            {
                int r = ns.Read(rd, 0, rd.Length);
                if (r <= 0) break;
                sw.Append(Encoding.ASCII.GetString(rd, 0, r));
            }
            Thread.Sleep(20);
        }
        Console.WriteLine("HS: " + sw.ToString().Split('\n')[0]);

        // masked text frame
        var pay = Encoding.UTF8.GetBytes(json);
        using (var ms = new MemoryStream())
        {
            ms.WriteByte(0x81);
            int len = pay.Length;
            if (len < 126) { ms.WriteByte((byte)(0x80 | len)); }
            else if (len <= 65535) { ms.WriteByte(0xFE); ms.WriteByte((byte)(len >> 8)); ms.WriteByte((byte)len); }
            else { ms.WriteByte(0xFF); ms.Write(BitConverter.GetBytes((long)len), 0, 8); }
            var mask = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            ms.Write(mask, 0, 4);
            for (int i = 0; i < len; i++) ms.WriteByte((byte)(pay[i] ^ mask[i % 4]));
            var fr = ms.ToArray();
            ns.Write(fr, 0, fr.Length);
        }
        Console.WriteLine("sent: " + json);

        // read a couple of frames back
        deadline = Environment.TickCount + 6000;
        while (Environment.TickCount < deadline && ns.DataAvailable || Environment.TickCount < deadline && tcp.Connected)
        {
            Thread.Sleep(300);
            if (!ns.DataAvailable) { if (Environment.TickCount > deadline - 4000) break; continue; }
            int r = ns.Read(rd, 0, rd.Length);
            if (r <= 0) break;
            // dump unmasked-ish content (server frames are unmasked)
            var body = r > 6 ? Encoding.UTF8.GetString(rd, 6, r - 6) : "";
            Console.WriteLine("recv[" + r + "]: " + body.Substring(0, Math.Min(200, body.Length)));
            break;
        }
        return 0;
    }
}

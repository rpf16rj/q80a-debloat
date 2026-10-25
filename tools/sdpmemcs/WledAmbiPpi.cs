using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

internal static class WledAmbiPpi
{
    [DllImport("libdisplay-capture-api.so.0", EntryPoint = "_Z10ppicaptureP13RequestedDataPhS1_i", CallingConvention = CallingConvention.Cdecl)]
    private static extern int PpiCapture(IntPtr rd, IntPtr ybuf, IntPtr cbuf, int bufsize);

    private const int LedCount = 226;
    private const int TopN = 72, RightN = 41, BottomN = 72, LeftN = 41;

    private static int Main(string[] args)
    {
        string wledIp = args.Length > 0 ? args[0] : "192.168.3.176";
        int wledPort = args.Length > 1 ? int.Parse(args[1]) : 4048;
        int maxFrames = args.Length > 2 ? int.Parse(args[2]) : -1;
        int w = 960, h = 540;

        var udp = new UdpClient();
        try
        {
            var wr = System.Net.WebRequest.Create("http://" + wledIp + "/json/state");
            wr.Method = "POST"; wr.ContentType = "application/json"; wr.Timeout = 3000;
            var body = System.Text.Encoding.UTF8.GetBytes("{\"seg\":[{\"fx\":0,\"col\":[[0,0,0],[0,0,0],[0,0,0]]}]}");
            using (var rs = wr.GetRequestStream()) rs.Write(body, 0, body.Length);
            wr.GetResponse().Close();
        }
        catch (Exception e) { Console.WriteLine("black-state: " + e.Message); }
        IntPtr rd = Marshal.AllocHGlobal(512);
        IntPtr ybuf = Marshal.AllocHGlobal(w * h);
        IntPtr cbuf = Marshal.AllocHGlobal(w * h);
        var zeros = new byte[512];
        Marshal.Copy(zeros, 0, rd, 512);
        Marshal.WriteInt32(rd, 0x00, w);
        Marshal.WriteInt32(rd, 0x04, h);
        Marshal.WriteInt32(rd, 0x1c, 0);
        Marshal.WriteInt32(rd, 0x24, 2);
        Marshal.WriteInt32(rd, 0x28, 6);

        var frame = 0; var sw = new Stopwatch(); long tot = 0;
        Console.WriteLine("WledAmbiPpi: " + wledIp + ":" + wledPort);

        while (maxFrames < 0 || frame < maxFrames)
        {
            sw.Restart();
            int r = PpiCapture(rd, ybuf, cbuf, w * h);
            if (r != 0) { Console.WriteLine("ppi fail r=" + r); System.Threading.Thread.Sleep(500); continue; }
            int rw = Marshal.ReadInt32(rd, 0), rh = Marshal.ReadInt32(rd, 4);
            IntPtr yp = Marshal.ReadIntPtr(rd, 0x14), cp = Marshal.ReadIntPtr(rd, 0x18);

            byte[] leds = SampleEdges(yp, cp, rw, rh);
            SendDdp(udp, wledIp, wledPort, leds);
            frame++;
            tot += sw.ElapsedMilliseconds;
            if (frame % 100 == 0) Console.WriteLine("f" + frame + " avg=" + tot / frame + "ms " + rw + "x" + rh);
        }
        return 0;
    }

    private static void SendDdp(UdpClient udp, string ip, int port, byte[] rgb)
    {
        var pkt = new byte[10 + rgb.Length];
        pkt[0] = 0x41; pkt[1] = 0x01; pkt[2] = 0x01; pkt[3] = 0x01;
        pkt[8] = (byte)(rgb.Length >> 8); pkt[9] = (byte)(rgb.Length & 0xff);
        Buffer.BlockCopy(rgb, 0, pkt, 10, rgb.Length);
        udp.Send(pkt, pkt.Length, ip, port);
    }

    // YUV422: Y plane w*h, Cb plane (w/2)*h, Cr plane (w/2)*h  (I422)
    private static unsafe byte[] SampleEdges(IntPtr yp, IntPtr cp, int w, int h)
    {
        var leds = new byte[LedCount * 3];
        int cw = w / 2;
        int strip = Math.Max(6, h / 30), stripX = Math.Max(6, w / 30);
        byte* Y = (byte*)yp, Cb = (byte*)cp;
        Sample(Y, Cb, w, h, cw, 0, 0, w, strip, true, leds, 0, TopN, false);
        Sample(Y, Cb, w, h, cw, w - stripX, 0, w, h, false, leds, TopN, RightN, false);
        Sample(Y, Cb, w, h, cw, 0, h - strip, w, h, true, leds, TopN + RightN, BottomN, true);
        Sample(Y, Cb, w, h, cw, 0, 0, stripX, h, false, leds, TopN + RightN + BottomN, LeftN, true);
        return leds;
    }

    private static unsafe void Sample(byte* Y, byte* Cb, int w, int h, int cw,
        int x0, int y0, int x1, int y1, bool horizontal, byte[] leds, int ledStart, int ledCount, bool reverse)
    {
        int span = horizontal ? (x1 - x0) : (y1 - y0);
        for (int i = 0; i < ledCount; i++)
        {
            int idx = reverse ? (ledCount - 1 - i) : i;
            int a = x0, b = y0, a1 = x1, b1 = y1;
            int s = i * span / ledCount, e = (i + 1) * span / ledCount;
            if (horizontal) { a = x0 + s; a1 = x0 + e; } else { b = y0 + s; b1 = y0 + e; }
            long rr = 0, gg = 0, bb = 0; int cnt = 0;
            for (int y = b; y < b1; y += 2)
                for (int x = a; x < a1; x += 2)
                {
                    int yy = Y[y * w + x];
                    int ci = (y / 2) * w + (x & ~1);  // NV16: CbCr interleaved, half vertical res
                    int cb = Cb[ci] - 128, cr = Cb[ci + 1] - 128;
                    int r = yy + (int)(1.402 * cr), g = yy - (int)(0.344 * cb + 0.714 * cr), bl = yy + (int)(1.772 * cb);
                    rr += Clamp(r); gg += Clamp(g); bb += Clamp(bl); cnt++;
                }
            if (cnt == 0) cnt = 1;
            int o2 = (ledStart + idx) * 3;
            leds[o2] = (byte)(rr / cnt); leds[o2 + 1] = (byte)(gg / cnt); leds[o2 + 2] = (byte)(bb / cnt);
        }
    }

    private static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
}

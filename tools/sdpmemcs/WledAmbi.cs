using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;

internal static class WledAmbi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Dcr
    {
        public int AppType, CaptureMode, CompressionType, Quality;
        public IntPtr DirectoryPath, FileName;
        public int Width, Height, ReturnWidth, ReturnHeight;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] ReturnPath;
    }

    [DllImport("libdisplay-capture-api.so.0", EntryPoint = "dc_request_capture_to_file_sync", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CaptureToFile(ref Dcr req);

    [DllImport("libturbojpeg.so.0", EntryPoint = "tjInitDecompress", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr TjInitDecompress();

    [DllImport("libturbojpeg.so.0", EntryPoint = "tjDecompressHeader3", CallingConvention = CallingConvention.Cdecl)]
    private static extern int TjHeader(IntPtr h, byte[] buf, int size, out int w, out int hh, out int sub, out int cs);

    [DllImport("libturbojpeg.so.0", EntryPoint = "tjDecompress2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int TjDecomp(IntPtr h, byte[] buf, int size, byte[] dst, int w, int pitch, int hh, int pixfmt, int flags);

    [DllImport("libturbojpeg.so.0", EntryPoint = "tjDestroy", CallingConvention = CallingConvention.Cdecl)]
    private static extern int TjDestroy(IntPtr h);

    private const int LedCount = 226;
    private const int TopN = 72, RightN = 41, BottomN = 72, LeftN = 41;
    private const int TjpfRgb = 0;

    private static int Main(string[] args)
    {
        string wledIp = args.Length > 0 ? args[0] : "192.168.3.176";
        int wledPort = args.Length > 1 ? int.Parse(args[1]) : 4048;
        string dir = args.Length > 2 ? args[2] : "/home/owner/share/caps";
        int sleepMs = args.Length > 3 ? int.Parse(args[3]) : 0;
        int maxFrames = args.Length > 4 ? int.Parse(args[4]) : -1;

        var udp = new UdpClient();
        var tj = TjInitDecompress();
        var frame = 0; var sw = new Stopwatch();
        long capMs = 0, decMs = 0, sendMs = 0;
        Console.WriteLine("WledAmbi v2: " + wledIp + ":" + wledPort + " jpeg-path");

        while (maxFrames < 0 || frame < maxFrames)
        {
            sw.Restart();
            var req = new Dcr();
            req.AppType = 0; req.CaptureMode = 0; req.CompressionType = 1; req.Quality = 85;
            req.DirectoryPath = Marshal.StringToHGlobalAnsi(dir);
            req.FileName = Marshal.StringToHGlobalAnsi("ambi");
            req.Width = 320; req.Height = 240; req.ReturnPath = new byte[256];
            int r = CaptureToFile(ref req);
            Marshal.FreeHGlobal(req.DirectoryPath); Marshal.FreeHGlobal(req.FileName);
            capMs += sw.ElapsedMilliseconds;
            if (r != 0) { Console.WriteLine("cap fail r=" + r); System.Threading.Thread.Sleep(300); continue; }

            string path = NullTerm(req.ReturnPath);
            byte[] jpg = File.ReadAllBytes(path);
            int w, h, sub, cs;
            if (TjHeader(tj, jpg, jpg.Length, out w, out h, out sub, out cs) != 0)
            { Console.WriteLine("jpeg hdr fail"); continue; }
            var rgb = new byte[w * h * 3];
            if (TjDecomp(tj, jpg, jpg.Length, rgb, w, w * 3, h, TjpfRgb, 0) != 0)
            { Console.WriteLine("jpeg dec fail"); continue; }
            decMs += sw.ElapsedMilliseconds - capMs;

            byte[] leds = SampleEdges(rgb, w, h, w * 3);
            SendDdp(udp, wledIp, wledPort, leds);
            sendMs += sw.ElapsedMilliseconds - capMs - decMs;
            frame++;
            if (frame % 50 == 0)
                Console.WriteLine("f" + frame + " tot=" + (capMs + decMs + sendMs) / frame + "ms cap=" + capMs / frame + " dec=" + decMs / frame + " " + w + "x" + h);
            if (sleepMs > 0) System.Threading.Thread.Sleep(sleepMs);
        }
        TjDestroy(tj);
        return 0;
    }

    private static string NullTerm(byte[] b)
    {
        int n = 0; while (n < b.Length && b[n] != 0) n++;
        return System.Text.Encoding.ASCII.GetString(b, 0, n);
    }

    private static void SendDdp(UdpClient udp, string ip, int port, byte[] rgb)
    {
        var pkt = new byte[10 + rgb.Length];
        pkt[0] = 0x41; pkt[1] = 0x01; pkt[2] = 0x01; pkt[3] = 0x01;
        pkt[8] = (byte)(rgb.Length >> 8); pkt[9] = (byte)(rgb.Length & 0xff);
        Buffer.BlockCopy(rgb, 0, pkt, 10, rgb.Length);
        udp.Send(pkt, pkt.Length, ip, port);
    }

    private static byte[] SampleEdges(byte[] rgb, int w, int h, int stride)
    {
        var leds = new byte[LedCount * 3];
        int strip = Math.Max(6, h / 30);
        int stripX = Math.Max(6, w / 30);
        SampleStrip(rgb, stride, 0, 0, w, strip, true, leds, 0, TopN, false);
        SampleStrip(rgb, stride, w - stripX, 0, w, h, false, leds, TopN, RightN, false);
        SampleStrip(rgb, stride, 0, h - strip, w, h, true, leds, TopN + RightN, BottomN, true);
        SampleStrip(rgb, stride, 0, 0, stripX, h, false, leds, TopN + RightN + BottomN, LeftN, true);
        return leds;
    }

    private static void SampleStrip(byte[] rgb, int stride, int x0, int y0, int x1, int y1,
        bool horizontal, byte[] leds, int ledStart, int ledCount, bool reverse)
    {
        int span = horizontal ? (x1 - x0) : (y1 - y0);
        for (int i = 0; i < ledCount; i++)
        {
            int idx = reverse ? (ledCount - 1 - i) : i;
            int a = x0, b = y0, a1 = x1, b1 = y1;
            int s = (int)((long)i * span / ledCount);
            int e = (int)((long)(i + 1) * span / ledCount);
            if (horizontal) { a = x0 + s; a1 = x0 + e; } else { b = y0 + s; b1 = y0 + e; }
            long rr = 0, gg = 0, bb = 0; int cnt = 0;
            for (int y = b; y < b1; y++)
                for (int x = a; x < a1; x++)
                {
                    int o = y * stride + x * 3;
                    rr += rgb[o]; gg += rgb[o + 1]; bb += rgb[o + 2]; cnt++;
                }
            if (cnt == 0) cnt = 1;
            int o2 = (ledStart + idx) * 3;
            leds[o2] = (byte)(rr / cnt); leds[o2 + 1] = (byte)(gg / cnt); leds[o2 + 2] = (byte)(bb / cnt);
        }
    }
}

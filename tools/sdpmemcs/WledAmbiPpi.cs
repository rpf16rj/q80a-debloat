using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

internal static class WledAmbiPpi
{
    [DllImport("libdisplay-capture-api.so.0", EntryPoint = "_Z10ppicaptureP13RequestedDataPhS1_i", CallingConvention = CallingConvention.Cdecl)]
    private static extern int PpiCapture(IntPtr rd, IntPtr ybuf, IntPtr cbuf, int bufsize);

    // ---- ppi_ve_* DRM fallback (libvideoenhance measure block) ----
    [DllImport("libdl.so.2", EntryPoint = "dlopen", CharSet = CharSet.Ansi)]
    private static extern IntPtr dlopen(string filename, int flags);
    [DllImport("libdl.so.2", EntryPoint = "dlsym", CharSet = CharSet.Ansi)]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CondFn(IntPtr buf);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PosFn(int slot, int x, int y);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PixFn(int slot, IntPtr buf);
    private static PosFn _pos; private static PixFn _pix;
    private static int _slots, _blkW, _blkH, _sleepMs, _fullW, _fullH;
    private static IntPtr _colBuf;
    private static bool _ppiReady, _ppiDead;

    private static bool PpiVeInit()
    {
        if (_ppiReady) return true;
        if (_ppiDead) return false;   // lib nao tem a API nessa firmware: tenta uma vez so
        IntPtr lib = dlopen("/usr/lib/libvideoenhance.so", 1);
        if (lib == IntPtr.Zero) lib = dlopen("libvideoenhance.so", 1);
        if (lib == IntPtr.Zero) { Console.WriteLine("ppiVe: dlopen fail"); return false; }
        IntPtr fc = dlsym(lib, "ppi_ve_get_rgb_measure_condition");
        IntPtr fp = dlsym(lib, "ppi_ve_set_rgb_measure_position");
        IntPtr fx = dlsym(lib, "ppi_ve_get_rgb_measure_pixel");
        if (fc == IntPtr.Zero || fp == IntPtr.Zero || fx == IntPtr.Zero)
        { _ppiDead = true; Console.WriteLine("ppiVe: dlsym fail (API ausente nesta firmware)"); return false; }
        IntPtr cb = Marshal.AllocHGlobal(256);
        Marshal.Copy(new byte[256], 0, cb, 256);
        int rc = Marshal.GetDelegateForFunctionPointer<CondFn>(fc)(cb);
        if (rc < 0) { Marshal.FreeHGlobal(cb); Console.WriteLine("ppiVe: cond rc=" + rc); return false; }
        _slots = Marshal.ReadInt32(cb, 0); _blkW = Marshal.ReadInt32(cb, 4);
        _blkH = Marshal.ReadInt32(cb, 8); _sleepMs = Marshal.ReadInt32(cb, 12);
        _fullW = Marshal.ReadInt32(cb, 16); _fullH = Marshal.ReadInt32(cb, 20);
        Marshal.FreeHGlobal(cb);
        if (_slots <= 0 || _fullW <= 0) { Console.WriteLine("ppiVe: bad cond"); return false; }
        if (_slots > 16) _slots = 16;
        if (_sleepMs < 1) _sleepMs = 20;
        _pos = Marshal.GetDelegateForFunctionPointer<PosFn>(fp);
        _pix = Marshal.GetDelegateForFunctionPointer<PixFn>(fx);
        _colBuf = Marshal.AllocHGlobal(256);
        _ppiReady = true;
        Console.WriteLine("ppiVe ready: slots=" + _slots + " block=" + _blkW + "x" + _blkH +
            " settle=" + _sleepMs + "ms full=" + _fullW + "x" + _fullH);
        return true;
    }

    // perimeter spot positions (fractions of frame) matching the LED layout
    private static readonly double[][] SpotFrac = new double[][] {
        new double[]{0.10,0.02}, new double[]{0.30,0.02}, new double[]{0.50,0.02}, new double[]{0.70,0.02}, new double[]{0.90,0.02},
        new double[]{0.98,0.15}, new double[]{0.98,0.50}, new double[]{0.98,0.85},
        new double[]{0.90,0.98}, new double[]{0.70,0.98}, new double[]{0.50,0.98}, new double[]{0.30,0.98}, new double[]{0.10,0.98},
        new double[]{0.02,0.85}, new double[]{0.02,0.50}, new double[]{0.02,0.15},
    };

    private static byte[] PpiVeFrame()
    {
        int n = SpotFrac.Length;
        var rgb = new int[n * 3];
        for (int base_ = 0; base_ < n; base_ += _slots)
        {
            int cnt = Math.Min(_slots, n - base_);
            for (int s = 0; s < cnt; s++)
            {
                int x = (int)(SpotFrac[base_ + s][0] * _fullW);
                int y = (int)(SpotFrac[base_ + s][1] * _fullH);
                _pos(s, x, y);
            }
            System.Threading.Thread.Sleep(_sleepMs);
            for (int s = 0; s < cnt; s++)
            {
                Marshal.Copy(new byte[256], 0, _colBuf, 256);
                if (_pix(s, _colBuf) >= 0)
                {
                    int i = base_ + s;
                    rgb[i * 3] = Marshal.ReadInt32(_colBuf, 0) >> 2;   // 10-bit -> 8-bit
                    rgb[i * 3 + 1] = Marshal.ReadInt32(_colBuf, 4) >> 2;
                    rgb[i * 3 + 2] = Marshal.ReadInt32(_colBuf, 8) >> 2;
                }
            }
        }
        // map 16 spots -> 226 leds (nearest spot in same layout order)
        var leds = new byte[LedCount * 3];
        int[] counts = { TopN, RightN, BottomN, LeftN };
        int[] spotN = { 5, 3, 5, 3 };
        int sp = 0, li = 0;
        for (int e = 0; e < 4; e++)
        {
            for (int i = 0; i < counts[e]; i++)
            {
                int si = sp + Math.Min(spotN[e] - 1, i * spotN[e] / counts[e]);
                leds[li * 3] = (byte)rgb[si * 3]; leds[li * 3 + 1] = (byte)rgb[si * 3 + 1]; leds[li * 3 + 2] = (byte)rgb[si * 3 + 2];
                li++;
            }
            sp += spotN[e];
        }
        return leds;
    }

    private const int LedCount = 226;
    private const int TopN = 72, RightN = 41, BottomN = 72, LeftN = 41;

    private static int Main(string[] args)
    {
        string wledIp = args.Length > 0 ? args[0] : "192.168.3.176";
        int wledPort = args.Length > 1 ? int.Parse(args[1]) : 4048;
        int maxFrames = args.Length > 2 ? int.Parse(args[2]) : -1;
        bool hdr = args.Length > 3 && args[3].StartsWith("hdr");
        double lumScale = 12.0, gamma = 2.2;
        if (hdr && args[3].Contains(":"))
        {
            var p = args[3].Split(':');
            if (p.Length > 1) double.TryParse(p[1], out lumScale);
            if (p.Length > 2) double.TryParse(p[2], out gamma);
        }
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

        var frame = 0; var sw = new Stopwatch(); long tot = 0; int drmStreak = 0, failStreak = 0;
        Console.WriteLine("WledAmbiPpi: " + wledIp + ":" + wledPort);

        while (maxFrames < 0 || frame < maxFrames)
        {
            sw.Restart();
            int r = PpiCapture(rd, ybuf, cbuf, w * h);
            if (r != 0)
            {
                if (r == -4) drmStreak += 4;          // DRM: entra rapido
                else if (r == -1) drmStreak++;        // HDCP/ACCESS_DENIED: precisa de streak maior (~10s)
                else drmStreak = 0;
                if (drmStreak >= 10 && PpiVeInit())
                {
                    var leds2 = PpiVeFrame();
                    SendDdp(udp, wledIp, wledPort, leds2);
                    if (drmStreak % 50 == 0) Console.WriteLine("ppiVe mode streak=" + drmStreak);
                    System.Threading.Thread.Sleep(150);
                    continue;
                }
                Console.WriteLine("ppi fail r=" + r); System.Threading.Thread.Sleep(500);
                // wedge (ex.: toggle VRR) trava o capture do processo pra sempre: sai pra o
                // launcher em loop respawnar um processo novo (sessao limpa = volta).
                if (++failStreak > 600) { Console.WriteLine("wedge: saindo p/ respawn"); return 2; }
                continue;
            }
            drmStreak = 0; failStreak = 0;
            int rw = Marshal.ReadInt32(rd, 0), rh = Marshal.ReadInt32(rd, 4);
            IntPtr yp = Marshal.ReadIntPtr(rd, 0x14), cp = Marshal.ReadIntPtr(rd, 0x18);

            byte[] leds = hdr ? SampleEdgesHdr(yp, cp, rw, rh, lumScale, gamma) : SampleEdges(yp, cp, rw, rh);
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
                    // YUV studio-range (16-235/16-240) + BT.709 -> expande pra full-range antes de converter
                    int yy = (Y[y * w + x] - 16) * 255 / 219;
                    int ci = (y / 2) * w + (x & ~1);  // NV12: CbCr interleaved, half res
                    int cb = (Cb[ci] - 128) * 255 / 224, cr = (Cb[ci + 1] - 128) * 255 / 224;
                    int r = yy + (int)(1.5748 * cr), g = yy - (int)(0.1873 * cb + 0.4681 * cr), bl = yy + (int)(1.8556 * cb);
                    rr += Clamp(r); gg += Clamp(g); bb += Clamp(bl); cnt++;
                }
            if (cnt == 0) cnt = 1;
            int o2 = (ledStart + idx) * 3;
            leds[o2] = (byte)(rr / cnt); leds[o2 + 1] = (byte)(gg / cnt); leds[o2 + 2] = (byte)(bb / cnt);
        }
    }

    private static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

    // ================= HDR (PQ/BT.2020) decode =================
    // code(0-255) -> linear light (0..1) via ST2084 PQ EOTF
    private static readonly double[] _pqLut = BuildPqLut();
    private static double[] BuildPqLut()
    {
        var t = new double[256];
        const double m1 = 0.159301758, m2 = 78.84375, c1 = 0.8359375, c2 = 18.8515625, c3 = 18.6875;
        for (int i = 0; i < 256; i++)
        {
            double v = i / 255.0;
            double p = Math.Pow(v, 1.0 / m2);
            double n = Math.Max(p - c1, 0) / (c2 - c3 * p);
            t[i] = Math.Pow(n, 1.0 / m1);   // 0..1 (1.0 = 10000 nits)
        }
        return t;
    }
    private static int PqIdx(double v) { return v < 0 ? 0 : (v > 1 ? 255 : (int)(v * 255 + 0.5)); }

    // PQ code -> linear, gamut 2020->709, tone map, gamma. ~ x4 faster via luts where possivel.
    private static unsafe void SampleHdr(byte* Y, byte* Cb, int w, int h, int cw,
        int x0, int y0, int x1, int y1, bool horizontal, byte[] leds, int ledStart, int ledCount, bool reverse,
        double lumScale, double gamma)
    {
        int span = horizontal ? (x1 - x0) : (y1 - y0);
        double invG = 1.0 / gamma;
        for (int i = 0; i < ledCount; i++)
        {
            int idx = reverse ? (ledCount - 1 - i) : i;
            int a = x0, b = y0, a1 = x1, b1 = y1;
            int s = i * span / ledCount, e = (i + 1) * span / ledCount;
            if (horizontal) { a = x0 + s; a1 = x0 + e; } else { b = y0 + s; b1 = y0 + e; }
            double rr = 0, gg = 0, bb = 0; int cnt = 0;
            for (int y = b; y < b1; y += 2)
                for (int x = a; x < a1; x += 2)
                {
                    // matriz BT.2020 NCL em code domain (chroma centrado em 128)
                    int ci = (y / 2) * w + (x & ~1);
                    double yc = Y[y * w + x] / 255.0;
                    double cb = (Cb[ci] - 128) / 255.0, cr = (Cb[ci + 1] - 128) / 255.0;
                    double rc = yc + 1.4746 * cr;
                    double gc = yc - 0.1646 * cb - 0.5714 * cr;
                    double bc = yc + 1.8814 * cb;
                    // PQ decode -> linear
                    double rl = _pqLut[PqIdx(rc)], gl = _pqLut[PqIdx(gc)], bl = _pqLut[PqIdx(bc)];
                    // gamut BT.2020 -> BT.709 (linear)
                    double r2 = 1.6605 * rl - 0.5877 * gl - 0.0728 * bl;
                    double g2 = -0.1246 * rl + 1.1329 * gl - 0.0083 * bl;
                    double b2 = -0.0182 * rl - 0.1006 * gl + 1.1187 * bl;
                    // tone map simples: escala + gamma encode
                    r2 *= lumScale; g2 *= lumScale; b2 *= lumScale;
                    rr += r2 <= 0 ? 0 : 255 * Math.Pow(r2 > 1 ? 1 : r2, invG);
                    gg += g2 <= 0 ? 0 : 255 * Math.Pow(g2 > 1 ? 1 : g2, invG);
                    bb += b2 <= 0 ? 0 : 255 * Math.Pow(b2 > 1 ? 1 : b2, invG);
                    cnt++;
                }
            if (cnt == 0) cnt = 1;
            int o2 = (ledStart + idx) * 3;
            leds[o2] = (byte)(rr / cnt); leds[o2 + 1] = (byte)(gg / cnt); leds[o2 + 2] = (byte)(bb / cnt);
        }
    }

    private static unsafe byte[] SampleEdgesHdr(IntPtr yp, IntPtr cp, int w, int h, double lumScale, double gamma)
    {
        var leds = new byte[LedCount * 3];
        int cw = w / 2;
        int strip = Math.Max(6, h / 30), stripX = Math.Max(6, w / 30);
        byte* Y = (byte*)yp, Cb = (byte*)cp;
        SampleHdr(Y, Cb, w, h, cw, 0, 0, w, strip, true, leds, 0, TopN, false, lumScale, gamma);
        SampleHdr(Y, Cb, w, h, cw, w - stripX, 0, w, h, false, leds, TopN, RightN, false, lumScale, gamma);
        SampleHdr(Y, Cb, w, h, cw, 0, h - strip, w, h, true, leds, TopN + RightN, BottomN, true, lumScale, gamma);
        SampleHdr(Y, Cb, w, h, cw, 0, 0, stripX, h, false, leds, TopN + RightN + BottomN, LeftN, true, lumScale, gamma);
        return leds;
    }
}

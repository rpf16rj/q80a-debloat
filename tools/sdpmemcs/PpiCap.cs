using System;
using System.Runtime.InteropServices;

internal static class PpiCap
{
    // _Z10ppicaptureP13RequestedDataPhS1_i = ppicapture(RequestedData* rd, uchar* ybuf, uchar* cbuf, int bufsize)
    [DllImport("libdisplay-capture-api.so.0", EntryPoint = "_Z10ppicaptureP13RequestedDataPhS1_i", CallingConvention = CallingConvention.Cdecl)]
    private static extern int PpiCapture(IntPtr rd, IntPtr ybuf, IntPtr cbuf, int bufsize);

    private static int Main(string[] args)
    {
        int w = args.Length > 0 ? int.Parse(args[0]) : 960;
        int h = args.Length > 1 ? int.Parse(args[1]) : 540;

        IntPtr rd = Marshal.AllocHGlobal(512);
        IntPtr ybuf = Marshal.AllocHGlobal(w * h);
        IntPtr cbuf = Marshal.AllocHGlobal(w * h);
        var zeros = new byte[512];
        Marshal.Copy(zeros, 0, rd, 512);

        // RequestedData layout from disasm of m_capture_to_file_sync:
        // +0x00 w, +0x04 h, +0x1c mode, +0x24 =2, +0x28 =6
        Marshal.WriteInt32(rd, 0x00, w);
        Marshal.WriteInt32(rd, 0x04, h);
        Marshal.WriteInt32(rd, 0x1c, 0);
        Marshal.WriteInt32(rd, 0x24, 2);
        Marshal.WriteInt32(rd, 0x28, 6);

        int r = PpiCapture(rd, ybuf, cbuf, w * h);
        Console.WriteLine("ppi r=" + r);
        int rw = Marshal.ReadInt32(rd, 0), rh = Marshal.ReadInt32(rd, 4);
        int ysz = Marshal.ReadInt32(rd, 0x0c), csz = Marshal.ReadInt32(rd, 0x10);
        IntPtr yp = Marshal.ReadIntPtr(rd, 0x14), cp = Marshal.ReadIntPtr(rd, 0x18);
        Console.WriteLine("rw=" + rw + " rh=" + rh + " ysz=" + ysz + " csz=" + csz +
            " yp=0x" + yp.ToString("x8") + " cp=0x" + cp.ToString("x8"));

        // read actual frame data from returned pointers
        var y = new byte[64];
        Marshal.Copy(yp, y, 0, 64);
        Console.WriteLine("y[0..63]=" + BitConverter.ToString(y));
        var cb = new byte[64];
        Marshal.Copy(cp, cb, 0, 64);
        Console.WriteLine("c[0..63]=" + BitConverter.ToString(cb));
        // variance check on luma middle
        var mid = new byte[4096];
        Marshal.Copy(IntPtr.Add(yp, rw * rh / 2), mid, 0, 4096);
        long sum = 0, sq = 0;
        foreach (var b in mid) { sum += b; sq += b * b; }
        double mean = sum / 4096.0, varr = sq / 4096.0 - mean * mean;
        Console.WriteLine("luma mean=" + mean.ToString("F1") + " var=" + varr.ToString("F1") + (varr > 10 ? " REAL-IMAGE" : " flat/black"));
        return 0;
    }
}

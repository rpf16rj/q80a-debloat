using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class CapBench
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayCaptureRequest
    {
        public int AppType;
        public int CaptureMode;
        public int CompressionType;
        public int Quality;
        public IntPtr DirectoryPath;
        public IntPtr FileName;
        public int Width;
        public int Height;
        public int ReturnWidth;
        public int ReturnHeight;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] ReturnPath;
    }

    [DllImport("libdisplay-capture-api.so.0", EntryPoint = "dc_request_capture_to_file_sync", CallingConvention = CallingConvention.Cdecl)]
    private static extern int DcRequestCaptureToFileSync(ref DisplayCaptureRequest request);

    private static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "/home/owner/share/caps";
        int n = args.Length > 1 ? int.Parse(args[1]) : 10;
        int w = args.Length > 2 ? int.Parse(args[2]) : 960;
        int h = args.Length > 3 ? int.Parse(args[3]) : 540;
        int comp = args.Length > 4 ? int.Parse(args[4]) : 0;

        var sw = new Stopwatch();
        long total = 0; int ok = 0, fail = 0; long maxMs = 0, minMs = long.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var req = new DisplayCaptureRequest();
            req.AppType = 0; req.CaptureMode = 0; req.CompressionType = comp; req.Quality = 90;
            req.DirectoryPath = Marshal.StringToHGlobalAnsi(dir);
            req.FileName = Marshal.StringToHGlobalAnsi("b" + i);
            req.Width = w; req.Height = h; req.ReturnPath = new byte[256];
            sw.Restart();
            int r = DcRequestCaptureToFileSync(ref req);
            sw.Stop();
            total += sw.ElapsedMilliseconds;
            if (sw.ElapsedMilliseconds > maxMs) maxMs = sw.ElapsedMilliseconds;
            if (sw.ElapsedMilliseconds < minMs) minMs = sw.ElapsedMilliseconds;
            if (r == 0) ok++; else fail++;
            Marshal.FreeHGlobal(req.DirectoryPath);
            Marshal.FreeHGlobal(req.FileName);
        }
        Console.WriteLine("n=" + n + " ok=" + ok + " fail=" + fail +
            " avg=" + (total / (double)Math.Max(n,1)).ToString("F1") + "ms" +
            " min=" + minMs + " max=" + maxMs +
            " fps=" + (1000.0 / Math.Max(total / (double)Math.Max(n,1), 0.001)).ToString("F1"));
        return 0;
    }
}

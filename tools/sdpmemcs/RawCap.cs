using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal static class RawCap
{
    // dc_request_capture_sync(app_type, mode, w, h, ybuf, cbuf) — probable sig
    [DllImport("libdisplay-capture-api.so.0", EntryPoint = "dc_request_capture_sync", CallingConvention = CallingConvention.Cdecl)]
    private static extern int DcSync0(int appType, int mode, int w, int h, IntPtr ybuf, IntPtr cbuf);

    private static int Main(string[] args)
    {
        int w = args.Length > 0 ? int.Parse(args[0]) : 720;
        int h = args.Length > 1 ? int.Parse(args[1]) : 540;
        int n = args.Length > 2 ? int.Parse(args[2]) : 10;

        IntPtr ybuf = Marshal.AllocHGlobal(4 * 1024 * 1024);
        IntPtr cbuf = Marshal.AllocHGlobal(4 * 1024 * 1024);

        // sanity: fill with 0xAA to detect writes
        var aa = new byte[64];
        for (int k = 0; k < aa.Length; k++) aa[k] = 0xAA;
        Marshal.Copy(aa, 0, ybuf, 64); Marshal.Copy(aa, 0, cbuf, 64);

        var sw = new Stopwatch();
        for (int i = 0; i < n; i++)
        {
            sw.Restart();
            int r = DcSync0(0, 0, w, h, ybuf, cbuf);
            sw.Stop();
            if (i < 3)
            {
                var y = new byte[16]; Marshal.Copy(ybuf, y, 0, 16);
                var cb = new byte[16]; Marshal.Copy(cbuf, cb, 0, 16);
                Console.WriteLine("try" + i + " r=" + r + " ms=" + sw.ElapsedMilliseconds +
                    " y=" + BitConverter.ToString(y) + " c=" + BitConverter.ToString(cb));
            }
            else if (i == n - 1)
            {
                Console.WriteLine("last r=" + r + " ms=" + sw.ElapsedMilliseconds);
            }
        }
        Marshal.FreeHGlobal(ybuf); Marshal.FreeHGlobal(cbuf);
        return 0;
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class PpiDump
{
    [DllImport("libdisplay-capture-api.so.0", EntryPoint = "_Z10ppicaptureP13RequestedDataPhS1_i", CallingConvention = CallingConvention.Cdecl)]
    private static extern int PpiCapture(IntPtr rd, IntPtr ybuf, IntPtr cbuf, int bufsize);

    private static int Main(string[] args)
    {
        int w = 960, h = 540;
        IntPtr rd = Marshal.AllocHGlobal(512);
        IntPtr ybuf = Marshal.AllocHGlobal(w * h);
        IntPtr cbuf = Marshal.AllocHGlobal(w * h);
        Marshal.Copy(new byte[512], 0, rd, 512);
        Marshal.WriteInt32(rd, 0x00, w); Marshal.WriteInt32(rd, 0x04, h);
        Marshal.WriteInt32(rd, 0x1c, 0); Marshal.WriteInt32(rd, 0x24, 2); Marshal.WriteInt32(rd, 0x28, 6);
        int r = PpiCapture(rd, ybuf, cbuf, w * h);
        int rw = Marshal.ReadInt32(rd, 0), rh = Marshal.ReadInt32(rd, 4);
        int ysz = Marshal.ReadInt32(rd, 0x0c), csz = Marshal.ReadInt32(rd, 0x10);
        IntPtr yp = Marshal.ReadIntPtr(rd, 0x14), cp = Marshal.ReadIntPtr(rd, 0x18);
        Console.WriteLine("r=" + r + " " + rw + "x" + rh + " ysz=" + ysz + " csz=" + csz);
        var y = new byte[ysz]; Marshal.Copy(yp, y, 0, ysz);
        var c = new byte[csz]; Marshal.Copy(cp, c, 0, csz);
        File.WriteAllBytes("/home/owner/share/frame.y", y);
        File.WriteAllBytes("/home/owner/share/frame.c", c);
        Console.WriteLine("dumped " + ysz + "+" + csz);
        return 0;
    }
}

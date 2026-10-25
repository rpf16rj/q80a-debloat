using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class SdpMemProbe
{
    private const int OpenReadWrite = 2;
    private const int ProtectRead = 1;
    private const int MapShared = 1;
    private const int SysMmap2Arm = 192;
    private const int PageSize = 4096;
    private const int RtldNow = 2;

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern IntPtr DlOpen(string f, int flags);
    [DllImport("libdl.so.2", EntryPoint = "dlsym")]
    private static extern IntPtr DlSym(IntPtr h, string name);

    private delegate int OpenDelegate(string path, int flags, int mode);
    private delegate IntPtr SyscallDelegate(int number, IntPtr a1, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6);
    private delegate int MunmapDelegate(IntPtr addr, IntPtr len);
    private delegate int CloseDelegate(int fd);

    private static string[] Devices = {
        "/dev/sdp_mem", "/dev/sdp_hwmem", "/dev/sdp_kade",
        "/dev/sdpfrc", "/dev/sdp_ddrbus", "/dev/bptime"
    };
    private static long[] Pfns = { 0x40000, 0x50000, 0x80000, 0xa4000, 0x20000, 0x18000 };

    private static int Main()
    {
        var libc = DlOpen("libc.so.6", RtldNow);
        if (libc == IntPtr.Zero) { Console.WriteLine("no libc"); return 1; }
        var open = Marshal.GetDelegateForFunctionPointer<OpenDelegate>(DlSym(libc, "open"));
        var syscall = Marshal.GetDelegateForFunctionPointer<SyscallDelegate>(DlSym(libc, "syscall"));
        var munmap = Marshal.GetDelegateForFunctionPointer<MunmapDelegate>(DlSym(libc, "munmap"));
        var close = Marshal.GetDelegateForFunctionPointer<CloseDelegate>(DlSym(libc, "close"));

        foreach (var dev in Devices)
        {
            int fd = open(dev, OpenReadWrite, 0);
            if (fd < 0) { Console.WriteLine(dev + ": open fail"); continue; }
            foreach (var pfn in Pfns)
            {
                IntPtr p = syscall(SysMmap2Arm, IntPtr.Zero, (IntPtr)PageSize,
                    (IntPtr)ProtectRead, (IntPtr)MapShared, (IntPtr)fd, (IntPtr)pfn);
                Console.Write(dev + " pfn=0x" + pfn.ToString("x") + " => ");
                if (p.ToInt32() == -1) { Console.WriteLine("ERR"); continue; }
                Console.Write("0x" + p.ToString("x8") + " data:");
                try
                {
                    var buf = new byte[16];
                    Marshal.Copy(p, buf, 0, 16);
                    Console.WriteLine(BitConverter.ToString(buf));
                }
                catch (Exception e) { Console.WriteLine("read-exc " + e.GetType().Name); }
                munmap(p, (IntPtr)PageSize);
            }
            close(fd);
        }
        return 0;
    }
}

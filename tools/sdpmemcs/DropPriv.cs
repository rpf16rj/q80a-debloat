using System;
using System.Runtime.InteropServices;

internal static class DropPriv
{
    [DllImport("libc", SetLastError = true)]
    private static extern int setgid(int gid);
    [DllImport("libc", SetLastError = true)]
    private static extern int setuid(int uid);
    [DllImport("libc", SetLastError = true)]
    private static extern int initgroups(string user, int gid);
    [DllImport("libc", SetLastError = true)]
    private static extern int system(string cmd);

    private static int Main(string[] args)
    {
        int uid = args.Length > 0 ? int.Parse(args[0]) : 5001;
        int gid = args.Length > 1 ? int.Parse(args[1]) : 100;
        string cmd = args.Length > 2 ? string.Join(" ", args, 2, args.Length - 2) : "id";
        initgroups("owner", gid);
        Console.WriteLine("setgid=" + setgid(gid) + " setuid=" + setuid(uid) + " -> " + cmd);
        int r = system(cmd);
        Console.WriteLine("system rc=" + r);
        return 0;
    }
}

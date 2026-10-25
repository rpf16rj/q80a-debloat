using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class ScreenCap
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
        string dir = args.Length > 0 ? args[0] : "/tmp";
        for (int appType = 0; appType <= 3; appType++)
        {
            for (int mode = 0; mode <= 3; mode++)
            {
                var req = new DisplayCaptureRequest();
                req.AppType = appType;
                req.CaptureMode = mode;
                req.CompressionType = 0;
                req.Quality = 90;
                req.DirectoryPath = Marshal.StringToHGlobalAnsi(dir);
                req.FileName = Marshal.StringToHGlobalAnsi("cap_" + appType + "_" + mode + ".jpg");
                req.Width = 960;
                req.Height = 540;
                req.ReturnPath = new byte[256];
                int r = DcRequestCaptureToFileSync(ref req);
                string path = NullTerm(req.ReturnPath);
                long sz = 0;
                try { if (path.Length > 0 && File.Exists(path)) sz = new FileInfo(path).Length; } catch {}
                Console.WriteLine("app=" + appType + " mode=" + mode + " => r=" + r +
                    " w=" + req.ReturnWidth + " h=" + req.ReturnHeight +
                    " size=" + sz + " path=" + path);
                Marshal.FreeHGlobal(req.DirectoryPath);
                Marshal.FreeHGlobal(req.FileName);
            }
        }
        return 0;
    }

    private static string NullTerm(byte[] b)
    {
        int n = 0;
        while (n < b.Length && b[n] != 0) n++;
        return Encoding.ASCII.GetString(b, 0, n);
    }
}

using System;
using System.Net.Sockets;
using System.Text;

internal static class UdpTest
{
    private static int Main(string[] args)
    {
        string ip = args.Length > 0 ? args[0] : "192.168.3.200";
        int port = args.Length > 1 ? int.Parse(args[1]) : 8891;
        try
        {
            var udp = new UdpClient();
            var data = Encoding.ASCII.GetBytes("DOTNET_UDP_OK");
            int n = udp.Send(data, data.Length, ip, port);
            Console.WriteLine("sent=" + n + " to " + ip + ":" + port);
            // DDP test: 3 red LEDs to WLED
            if (args.Length > 2 && args[2] == "ddp")
            {
                var pkt = new byte[10 + 9];
                pkt[0] = 0x41; pkt[1] = 0x01; pkt[2] = 0x01; pkt[3] = 0x01;
                pkt[8] = 0; pkt[9] = 9;
                for (int i = 10; i < pkt.Length; i += 3) { pkt[i] = 0xff; }
                int m = udp.Send(pkt, pkt.Length, ip, port);
                Console.WriteLine("ddp sent=" + m);
            }
        }
        catch (Exception e) { Console.WriteLine("ERR: " + e); }
        return 0;
    }
}

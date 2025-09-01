using System;
using System.Net.Sockets;
using System.Text;

class UdpTest
{
    static void Main()
    {
        try
        {
            UdpClient udp = new UdpClient(9002);
            Console.WriteLine("Listening on UDP port 9002...");
            while (true)
            {
                var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                var result = udp.Receive(ref endpoint);
                Console.WriteLine("Received " + result.Length + " bytes from " + endpoint + ": " + BitConverter.ToString(result));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
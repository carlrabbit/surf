using System.Net;
using System.Net.Sockets;
// TODO: format scripts as well
Console.WriteLine("test");


// var _cli = new TcpClient();
// _cli.Connect(new IPEndPoint(IPAddress.Loopback, 2222));
// _cli.GetStream().Write(new byte[] { 2, 3, 4 }, 0, 3);
// _cli.Close();
// _cli.Dispose();


Console.WriteLine(new UdpClient().Send(new byte[] { 2, 3, 4 }, 3, new IPEndPoint(IPAddress.Loopback, 2223)));

class X
{
    void test()
    {
        if (true)
        {

        }
    }
}

if (true)
{ // should 
    Console.WriteLine("TRUE");
}

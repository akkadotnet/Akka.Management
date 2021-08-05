using System.Net;
using System.Net.Sockets;

namespace Akka.Management.Cluster.Bootstrap.Tests
{
    public static class SocketUtil
    {
        public static IPEndPoint TemporaryTcpAddress(string hostName)
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(hostName), 0);
                socket.Bind(endpoint);
                return (IPEndPoint) socket.LocalEndPoint;
            }
        }
    }
}
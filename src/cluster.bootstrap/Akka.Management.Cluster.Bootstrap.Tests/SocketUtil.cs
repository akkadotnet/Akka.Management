//-----------------------------------------------------------------------
// <copyright file="SocketUtil.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

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
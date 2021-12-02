//-----------------------------------------------------------------------
// <copyright file="InvalidHealthCheckException.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Runtime.Serialization;

namespace Akka.Management
{
    public sealed class InvalidHealthCheckException : Exception
    {
        public InvalidHealthCheckException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public InvalidHealthCheckException(string message) : base(message)
        {
        }

        public InvalidHealthCheckException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
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
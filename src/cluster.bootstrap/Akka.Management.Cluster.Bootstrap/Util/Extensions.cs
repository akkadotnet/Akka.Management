using System;

namespace Akka.Management.Cluster.Bootstrap.Util
{
    public static class DateTimeOffsetExtensions
    {
        public static bool IsOverdue(this DateTimeOffset deadline)
            => deadline < DateTimeOffset.Now;
    }
}
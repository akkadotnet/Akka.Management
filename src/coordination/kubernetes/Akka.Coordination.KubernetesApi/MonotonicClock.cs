using System.Diagnostics;

namespace Akka.Coordination.KubernetesApi
{
    public static class MonotonicClock
    {
        private const double TicksInMilliSeconds = 10_000;
        
        private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        public static long Ticks => Stopwatch.ElapsedTicks;

        public static double ToMilliSeconds(this long ticks)
            => ticks / TicksInMilliSeconds;
    }
}
namespace Akka
{
    public static class Predef
    {
        /// <summary>
        /// Identity function to conform with the JVM API
        /// </summary>
        public static T Identity<T>(T x) => x;
    }
}
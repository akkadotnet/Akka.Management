using Akka.Annotations;

namespace Akka.Http.Dsl.Model
{
    /// <summary>
    /// The base type representing Http headers. All actual header values will be instances
    /// of one of the subtypes defined in the `headers` packages. Unknown headers will be subtypes
    /// of <see cref="RawHeader"/>. Not for user extension.
    /// </summary>
    [DoNotInherit]
    public abstract class HttpHeader
    {
        /// <summary>
        /// Returns the name of the header.
        /// </summary>
        public abstract string Name { get; }
        
        /// <summary>
        /// Returns the String representation of the value of the header.
        /// </summary>
        public abstract string Value { get; }
    }

    [DoNotInherit]
    public sealed class RawHeader : HttpHeader
    {
        private RawHeader(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string Name { get; }
        public override string Value { get; }

        public static RawHeader Create(string name, string value) =>
            new RawHeader(name, value);
    }
}
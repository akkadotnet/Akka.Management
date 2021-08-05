using System;

namespace Akka.Http.Dsl.Model
{
    public class AttributeKey<T>
    {
        public AttributeKey(string name)
        {
            Name = name;
            Type = typeof(T);
        }

        public string Name { get; }
        public Type Type { get; }
    }
}
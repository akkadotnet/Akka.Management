//-----------------------------------------------------------------------
// <copyright file="AttributeKey.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

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
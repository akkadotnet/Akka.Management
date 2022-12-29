//-----------------------------------------------------------------------
// <copyright file="EnumerableExtensions.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Route = System.ValueTuple<string, Akka.Http.Dsl.IAkkaHttpModule>;

namespace Akka.Http.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Generic Knuth shuffle algorithm for <see cref="List{T}"/>
        /// </summary>
        /// <param name="list">The <see cref="List{T}"/> to be shuffled</param>
        /// <typeparam name="T">Generic type</typeparam>
        /// <returns>The same list being shuffled</returns>
        public static List<T> Shuffle<T>(this List<T> list)
        {
            var rng = new Random();
            for (var i = list.Count - 1; i > -1; i--)
            {
                var j = rng.Next(0, i+1);
                (list[i], list[j]) = (list[j], list[i]); // swap the 2 indices
            }

            return list;
        }
    }
}
// -----------------------------------------------------------------------
//  <copyright file="XunitExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Xunit;

namespace Akka.Discovery.Azure.Tests.Utils
{
    public static class XunitExtensions
    {
        // converted from a BeAfter(expected - epsilon) + BeBefore(expected + epsilon) assertion (strict bounds)
        public static void BeApproximately(
            this DateTime subject,
            DateTime expected,
            TimeSpan epsilon)
        {
            Assert.True(subject > expected - epsilon);
            Assert.True(subject < expected + epsilon);
        }
    }
}

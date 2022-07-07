// -----------------------------------------------------------------------
//  <copyright file="XunitExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using FluentAssertions;
using FluentAssertions.Primitives;

namespace Akka.Discovery.Azure.Tests.Utils
{
    public static class XunitExtensions
    {
        public static AndConstraint<DateTimeAssertions> BeApproximately(
            this DateTimeAssertions assertion, 
            DateTime expected, 
            TimeSpan epsilon)
        {
            assertion.Subject.Should().BeAfter(expected - epsilon).And.BeBefore(expected + epsilon);
            return new AndConstraint<DateTimeAssertions>(assertion);
        }
    }
}
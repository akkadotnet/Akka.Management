//-----------------------------------------------------------------------
// <copyright file="HttpContactPointBootstrapSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;
using Akka.Management.Cluster.Bootstrap.Internal;
using FluentAssertions;
using Xunit;

namespace Akka.Management.Tests.Cluster.Bootstrap.Internal
{
    public class HttpContactPointBootstrapSpec
    {
        [Fact(DisplayName = "HttpContactPointBootstrap should use a safe name when connecting over IPv6")]
        public void ShouldUseSafeName()
        {
            var name = HttpContactPointBootstrap.Name("[fe80::1013:2070:258a:c662]", 443);
            ActorPath.IsValidPathElement(name).Should().BeTrue();
        }
    }
}
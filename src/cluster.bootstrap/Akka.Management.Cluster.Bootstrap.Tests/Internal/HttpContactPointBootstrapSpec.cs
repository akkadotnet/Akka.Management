using Akka.Actor;
using Akka.Management.Cluster.Bootstrap.Internal;
using FluentAssertions;
using Xunit;

namespace Akka.Management.Cluster.Bootstrap.Tests.Internal
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
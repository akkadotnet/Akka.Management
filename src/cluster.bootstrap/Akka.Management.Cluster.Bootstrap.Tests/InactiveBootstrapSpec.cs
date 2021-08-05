using System.Threading.Tasks;
using Akka.Actor;
using Xunit;

namespace Akka.Management.Cluster.Bootstrap.Tests
{
    public class InactiveBootstrapSpec : IAsyncLifetime
    {
        private ActorSystem _system;

        [Fact(DisplayName = "cluster-bootstrap on the classpath should not fail management routes if bootstrap is not configured or used")]
        public void NotFailManagementRoutesIdBootstrapIsNotConfiguredOrUsed()
        {
            // this will call ClusterBootstrap(system) which should not fail even if discovery is not configured
            AkkaManagement.Get(_system);
        }

        public Task InitializeAsync()
        {
            _system = ActorSystem.Create("InactiveBootstrapSpec");
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _system.Terminate();
        }
    }
}
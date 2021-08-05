using Akka.Actor;
using Akka.Http.Dsl;

namespace Akka.Management
{
    /// <summary>
    /// Extend this abstract class in your extension in order to allow it to contribute routes to Akka Management starts its HTTP endpoint
    /// </summary>
    public interface IManagementRouteProvider
    {
        /// <summary>
        /// Routes to be exposed by Akka cluster management
        /// </summary>
        Route[] Routes(ManagementRouteProviderSettings settings);
    }
}

using Akka.Actor;
using Akka.Http.Dsl.Server;

namespace Akka.Management
{
    /// <summary>
    /// Extend this abstract class in your extension in order to allow it to contribute routes to Akka Management starts its HTTP endpoint
    /// </summary>
    public abstract class ManagementRouteProvider : IExtension
    {
        /// <summary>
        /// Routes to be exposed by Akka cluster management
        /// </summary>
        public abstract Route[] Routes(ManagementRouteProviderSettings settings);
    }
}

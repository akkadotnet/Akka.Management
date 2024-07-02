using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Http.Dsl;
using Akka.Management.Dsl;
using Akka.Remote;
using Ceen;
using Newtonsoft.Json;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Management.Routes;

public class ClusterClientReceptionistRouteProvider: IManagementRouteProvider
{
    public Route[] Routes(ManagementRouteProviderSettings settings)
    {
        return new HttpClusterClientReceptionistRoutes().Routes;
    }
}

internal class HttpClusterClientReceptionistRoutes : HttpModuleBase
{
    public Route[] Routes => new Route[] { ("/cluster-client/receptionist", this) };
    
    public override async Task<bool> HandleAsync(IAkkaHttpContext context)
    {
        if (context.HttpContext.Request.Method.ToLowerInvariant() != "get")
            return false;
        
        // Check that remoting is in effect
        if (((ExtendedActorSystem)context.ActorSystem).Provider is not IRemoteActorRefProvider actorProvider)
        {
            context.HttpContext.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
            var response = JsonConvert.SerializeObject(new
            {
                error = new
                {
                    reason = "not available",
                    message = "Remoting is not available"
                },
                code = (int)HttpStatusCode.ServiceUnavailable,
                message = "Remoting is not available"
            });
            await context.HttpContext.Response.WriteAllJsonAsync(response);
            return true;
        }
        
        // Check that clustering is in effect
        if (((ExtendedActorSystem)context.ActorSystem).Provider is not IClusterActorRefProvider)
        {
            context.HttpContext.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
            var response = JsonConvert.SerializeObject(new
            {
                error = new
                {
                    reason = "not available",
                    message = "Clustering is not available"
                },
                code = (int)HttpStatusCode.ServiceUnavailable,
                message = "Clustering is not available"
            });
            await context.HttpContext.Response.WriteAllJsonAsync(response);
            return true;
        }
        
        // Check that ClusterClientReceptionist is available
        var config = context.ActorSystem.Settings.Config.GetConfig("akka.cluster.client.receptionist");
        if(config is null)
        {
            context.HttpContext.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
            var response = JsonConvert.SerializeObject(new
            {
                error = new
                {
                    reason = "not available",
                    message = "ClusterClientReceptionist is not available"
                },
                code = (int)HttpStatusCode.ServiceUnavailable,
                message = "ClusterClientReceptionist is not available"
            });
            await context.HttpContext.Response.WriteAllJsonAsync(response);
            return true;
        }

        // Check that ClusterClientReceptionist name is valid
        var name = config.GetString("name");
        if (string.IsNullOrWhiteSpace(name) || !ActorPath.IsValidPathElement(name))
        {
            context.HttpContext.Response.StatusCode = HttpStatusCode.InternalServerError;
            var response = JsonConvert.SerializeObject(new
            {
                error = new
                {
                    reason = "not available",
                    message = "ClusterClientReceptionist name is invalid"
                },
                code = (int)HttpStatusCode.InternalServerError,
                message = "ClusterClientReceptionist name is invalid"
            });
            await context.HttpContext.Response.WriteAllJsonAsync(response);
            return true;
        }
        
        var actorPath = new RootActorPath(actorProvider.DefaultAddress) / "system" / name; 
        var json = JsonConvert.SerializeObject(new { ReceptionistPath = actorPath.ToString() });
        await context.HttpContext.Response.WriteAllJsonAsync(json);
        
        return true;
    }
}
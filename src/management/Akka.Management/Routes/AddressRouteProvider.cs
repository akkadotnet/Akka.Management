using System.Threading.Tasks;
using Akka.Actor;
using Akka.Http.Dsl;
using Akka.Management.Dsl;
using Akka.Remote;
using Ceen;
using Newtonsoft.Json;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Management.Routes;

public class AddressRouteProvider: IManagementRouteProvider
{
    public Route[] Routes(ManagementRouteProviderSettings settings)
    {
        return new HttpAddressRoutes().Routes;
    }
}

internal class HttpAddressRoutes : HttpModuleBase
{
    public Route[] Routes => new Route[] { ("/remote/address", this) };
    
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
        
        var json = JsonConvert.SerializeObject(new
        {
            Address = actorProvider.DefaultAddress.ToString()
        });
        
        await context.HttpContext.Response.WriteAllJsonAsync(json);
        
        return true;
    }
}
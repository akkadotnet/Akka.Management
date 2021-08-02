using System.Net;
using Akka.Actor;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.Http.Extensions;
using Akka.IO;
using Akka.Management.Dsl;
using Akka.Management.Internal;
using Akka.Util;
using Microsoft.AspNetCore.Http;
using HttpResponse = Akka.Http.Dsl.Model.HttpResponse;

namespace Akka.Management
{
    public class HealthCheckRoutes : IManagementRouteProvider
    {
        private readonly HealthCheckSettings _settings;
        
        // exposed for testing
        internal HealthChecks HealthChecks { get; }
        
        public HealthCheckRoutes(ExtendedActorSystem system)
        {
            _settings = HealthCheckSettings.Create(system.Settings.Config.GetConfig("akka.management.health-checks"));
            HealthChecks = new HealthChecksImpl(system, _settings);
        }

        private HttpResponse HealthCheckResponse(Try<Either<string, Done>> result)
        {
            if (result.IsSuccess)
            {
                if (result.Success.Value is Left<string, Done> left)
                {
                    return HttpResponse.Create(
                        status: (int)HttpStatusCode.InternalServerError, 
                        entity: new RequestEntity(
                            contentType: ContentTypes.TextPlainUtf8,
                            ByteString.FromString($"Not healthy: {left.Value}")));
                }
                return HttpResponse.Create();
            }
            
            return HttpResponse.Create(
                status: (int)HttpStatusCode.InternalServerError, 
                entity: new RequestEntity(
                    contentType: ContentTypes.TextPlainUtf8,
                    ByteString.FromString($"Health Check Failed: {result.Failure}")));
        }
        
        public Route Routes(ManagementRouteProviderSettings settings)
        {
            return new Route[]
            {
                async context =>
                {
                    if (context.Request.Method != HttpMethods.Get || context.Request.Path != _settings.ReadinessPath)
                        return null;
                    return new RouteResult.Complete(HealthCheckResponse(await HealthChecks.ReadyResult()));
                },
                async context =>
                {
                    if (context.Request.Method != HttpMethods.Get || context.Request.Path != _settings.LivenessPath)
                        return null;
                    return new RouteResult.Complete(HealthCheckResponse(await HealthChecks.AliveResult()));
                },
            }.Concat();
        }
    }
}
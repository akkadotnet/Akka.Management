//-----------------------------------------------------------------------
// <copyright file="HealthCheckRoutes.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
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
        internal virtual HealthChecks HealthChecks { get; }
        
        public HealthCheckRoutes(ExtendedActorSystem system)
        {
            _settings = HealthCheckSettings.Create(system.Settings.Config.GetConfig("akka.management.health-checks"));
            HealthChecks = new HealthChecksImpl(system, _settings);
        }

        private async Task<HttpResponse> HealthCheckResponse(Func<Task<Either<string, Done>>> check)
        {
            try
            {
                var result = await check();
                if (result is Left<string, Done> left)
                    return HttpResponse.Create(
                        status: (int) HttpStatusCode.InternalServerError,
                        entity: new RequestEntity(
                            contentType: ContentTypes.TextPlainUtf8,
                            ByteString.FromString($"Not Healthy: {left.Value}")));
                return HttpResponse.Create();
            }
            catch (Exception e)
            {
                return HttpResponse.Create(
                    status: (int)HttpStatusCode.InternalServerError, 
                    entity: new RequestEntity(
                        contentType: ContentTypes.TextPlainUtf8,
                        ByteString.FromString($"Health Check Failed: {e.Message}")));
            }
        }
        
        public Route[] Routes(ManagementRouteProviderSettings settings)
        {
            return new Route[]
            {
                async context =>
                {
                    if (context.Request.Method != HttpMethods.Get || context.Request.Path != _settings.ReadinessPath)
                        return null;
                    return new RouteResult.Complete(await HealthCheckResponse(HealthChecks.ReadyResult));
                },
                async context =>
                {
                    if (context.Request.Method != HttpMethods.Get || context.Request.Path != _settings.LivenessPath)
                        return null;
                    return new RouteResult.Complete(await HealthCheckResponse(HealthChecks.AliveResult));
                },
            };
        }
    }
}
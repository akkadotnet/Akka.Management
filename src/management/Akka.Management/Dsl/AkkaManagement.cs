//-----------------------------------------------------------------------
// <copyright file="AkkaManagement.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Http;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Settings;
using Akka.Http.Extensions;
using Akka.Util;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Management.Dsl
{
    //using Route = Akka.Http.Dsl.Route;
    
    public class AkkaManagement : IExtension
    {
        private readonly ILoggingAdapter _log;
        private readonly ExtendedActorSystem _system;
        private readonly ImmutableList<IManagementRouteProvider> _routeProviders;
        private readonly AtomicReference<Task<ServerBinding>> _bindingFuture = new AtomicReference<Task<ServerBinding>>();

        public AkkaManagementSettings Settings { get; }

        public AkkaManagement(ExtendedActorSystem system)
        {
            _system = system;
            _log = Logging.GetLogger(system, GetType());

            system.Settings.InjectTopLevelFallback(AkkaManagementProvider.DefaultConfiguration());
            Settings = AkkaManagementSettings.Create(system.Settings.Config);

            var setup = _system.Settings.Setup.Get<AkkaManagementSetup>();
            if (setup.HasValue)
                Settings = setup.Value.Apply(Settings);

            _routeProviders = LoadRouteProviders().ToImmutableList();

            var coordinatedShutdown = CoordinatedShutdown.Get(system);
            coordinatedShutdown.AddTask(CoordinatedShutdown.PhaseBeforeClusterShutdown, "akka-management-exiting",
                () =>
                {
                    return Stop().ContinueWith(t => Done.Instance);
                });
            
            var autoStart = system.Settings.Config.GetStringList("akka.extensions")
                .Any(s => s.Contains(nameof(AkkaManagementProvider)));
            if (autoStart)
            {
                _log.Info("Akka.Management loaded through 'akka.extensions' auto starting bootstrap.");
                // Akka Management hosts the HTTP routes used by bootstrap
                // we can't let it block extension init, so run it in a different thread and let constructor complete
                Task.Run(async () =>
                {
                    try
                    {
                        await Get(system).Start();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e, "Failed to autostart cluster bootstrap, terminating system");
                        await system.Terminate();
                    }
                });
            }
        }

        public static AkkaManagement Get(ActorSystem system) => system.WithExtension<AkkaManagement, AkkaManagementProvider>();

        /// <summary>
        /// <para>Get the routes for the HTTP management endpoint.</para>
        /// <para>This method can be used to embed the Akka management routes in an existing Akka HTTP server.</para>
        /// </summary>
        public Route[] Routes() => PrepareCombinedRoutes(ProviderSettings());

        /// <summary>
        /// <para>Amend the <see cref="ManagementRouteProviderSettings"/> and get the routes for the HTTP management endpoint.</para>
        /// <para>Use this when adding authentication and HTTPS.</para>
        /// <para>This method can be used to embed the Akka management routes in an existing Akka HTTP server.</para>
        /// </summary>
        public Route[] Routes(Func<ManagementRouteProviderSettings, ManagementRouteProviderSettings> transformSettings) =>
            PrepareCombinedRoutes(transformSettings(ProviderSettings()));

        /// <summary>
        /// Start an Akka HTTP server to serve the HTTP management endpoint.
        /// </summary>
        public Task<Uri> Start() => Start(x => x);

        private Task<Uri> _startPromise;
        public Task<Uri> Start(Func<ManagementRouteProviderSettings, ManagementRouteProviderSettings> transformSettings)
        {
            if (_startPromise != null)
                return _startPromise;
            _startPromise = InternalStart(transformSettings);
            return _startPromise;
        }

        /// <summary>
        /// <para>Amend the <see cref="ManagementRouteProviderSettings"/> and start an Akka HTTP server to serve the HTTP management endpoint.</para>
        /// <para>Use this when adding authentication and HTTPS.</para>
        /// </summary>
        private async Task<Uri> InternalStart(Func<ManagementRouteProviderSettings, ManagementRouteProviderSettings> transformSettings)
        {
            var serverBindingPromise = new TaskCompletionSource<ServerBinding>();

            if (!_bindingFuture.CompareAndSet(null, serverBindingPromise.Task)) 
                return null;
            
            try
            {
                var effectiveBindHostname = Settings.Http.EffectiveBindHostname;
                var effectiveBindPort = Settings.Http.EffectiveBindPort;
                var effectiveProviderSettings = transformSettings(ProviderSettings());

                _log.Info("Binding Akka Management (HTTP) endpoint to: {0}:{1}", effectiveBindHostname, effectiveBindPort);

                var combinedRoutes = PrepareCombinedRoutes(effectiveProviderSettings);

                var baseBuilder = _system.Http()
                    .NewServerAt(effectiveBindHostname, effectiveBindPort)
                    .WithSettings(ServerSettings.Create(_system));

                var serverBinding = await baseBuilder.Bind(combinedRoutes).ConfigureAwait(false);
                
                serverBindingPromise.SetResult(serverBinding);

                var (boundAddress, boundPort) = serverBinding.LocalAddress switch
                {
                    DnsEndPoint ep => (ep.Host, ep.Port),
                    IPEndPoint ep => (ep.Address.ToString(), ep.Port),
                    _ => throw new Exception($"Unknown endpoint type: {serverBinding.LocalAddress.GetType()}")
                };
                _log.Info("Bound Akka Management (HTTP) endpoint to: {0}:{1}", boundAddress, boundPort);

                return effectiveProviderSettings.SelfBaseUri.WithPort(boundPort);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, ex.Message);
                throw new InvalidOperationException("Failed to start Akka Management HTTP endpoint.", ex);
            }
        }

        public Task<Done> Stop()
        {
            while (true)
            {
                var binding = _bindingFuture.Value;
                if (binding == null)
                {
                    return Task.FromResult(Done.Instance);
                }

                if (!_bindingFuture.CompareAndSet(binding, null))
                {
                    // retry, CAS was not successful, someone else completed the stop()
                    continue;
                }
                var stopFuture = binding.Map(_ => _.Unbind()).Map(_ => Done.Instance);
                _log.Info("Akka Management Stopped.");
                return stopFuture;
            }
        }

        private ManagementRouteProviderSettings ProviderSettings()
        {
            // port is on purpose never inferred from protocol, because this HTTP endpoint is not the "main" one for the app
            const string protocol = "http"; // changed to "https" if ManagementRouteProviderSettings.withHttpsConnectionContext is use

            var basePath = !string.IsNullOrWhiteSpace(Settings.Http.BasePath) ? Settings.Http.BasePath + "/" : string.Empty;
            var hostName = IsIPv6(Settings.Http.Hostname) ? $"[{Settings.Http.Hostname}]" : Settings.Http.Hostname;
            var selfBaseUri = new Uri($"{protocol}://{hostName}:{Settings.Http.Port}{basePath}");
            return ManagementRouteProviderSettings.Create(selfBaseUri, Settings.Http.RouteProvidersReadOnly);
        }

        private static bool IsIPv6(string hostName)
            => hostName.Count(c => c == ':') == 7;

        private Route[] PrepareCombinedRoutes(ManagementRouteProviderSettings providerSettings)
        {
            // TODO
            static Route[] WrapWithAuthenticatorIfPresent(Route[] inner)
            {
                return inner;
            }
            
            if(_routeProviders.IsEmpty)
                throw new ArgumentException("No routes configured for akka management! Double check your `akka.management.http.routes` config.");

            var combinedRoutes = _routeProviders
                .SelectMany(provider =>
                {
                    _log.Info("Including HTTP management routes for {0}", Logging.SimpleName(provider));
                    return provider.Routes(providerSettings);
                }).ToArray();

            return WrapWithAuthenticatorIfPresent(combinedRoutes);
        }

        private IEnumerable<IManagementRouteProvider> LoadRouteProviders()
        {
            foreach (var (name, fqcn) in Settings.Http.RouteProviders)
            {
                // Skip null or empty fqcn
                if(string.IsNullOrWhiteSpace(fqcn))
                    continue;
                
                var type = Type.GetType(fqcn);
                if (type == null)
                    throw new ConfigurationException($"Could not load Type from FQCN [{fqcn}]");

                if (typeof(IExtensionId).IsAssignableFrom(type))
                {
                    if (!_system.TryGetExtension(type, out var extension))
                    {
                        try
                        {
                            try
                            {
                                extension = Activator.CreateInstance(type);
                            }
                            catch (MissingMethodException)
                            {
                                extension = Activator.CreateInstance(type, _system);
                            }
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"While trying to load route provider extension [{name} = {fqcn}]", e);
                        }
                        
                        extension = _system.RegisterExtension((IExtensionId)extension);
                    }

                    yield return (IManagementRouteProvider)extension;
                }
                else
                {
                    if (!typeof(IManagementRouteProvider).IsAssignableFrom(type))
                        throw new ArgumentException(nameof(fqcn), $"[{fqcn}] is not a 'ManagementRouteProvider'");

                    IManagementRouteProvider instance;
                    try
                    {
                        try
                        {
                            instance = (IManagementRouteProvider) Activator.CreateInstance(type);
                        }
                        catch (MissingMethodException)
                        {
                            instance = (IManagementRouteProvider) Activator.CreateInstance(type, _system);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"While trying to load route provider extension [{name} = {fqcn}]", e);
                    }

                    yield return instance;
                }
            }
        }
    }

    public class AkkaManagementProvider : ExtensionIdProvider<AkkaManagement>
    {
        public override AkkaManagement CreateExtension(ExtendedActorSystem system) => new AkkaManagement(system);

        /// <summary>
        /// Returns a default configuration for the Akka Management module.
        /// </summary>
        public static Config DefaultConfiguration() => ConfigurationFactory.FromResource<AkkaManagement>("Akka.Management.Resources.reference.conf");
    }
}
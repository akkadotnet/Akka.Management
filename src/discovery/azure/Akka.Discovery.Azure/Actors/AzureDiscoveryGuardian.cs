// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoveryGuardian.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Threading;
using Akka.Actor;
using Akka.Event;
using Akka.Management;

namespace Akka.Discovery.Azure.Actors
{
    internal sealed class AzureDiscoveryGuardian: UntypedActor, IWithUnboundedStash
    {
        public static Props Props(AzureDiscoverySettings settings)
            => Actor.Props.Create(() => new AzureDiscoveryGuardian(settings)).WithDeploy(Deploy.Local);

        private readonly ILoggingAdapter _log;
        private readonly AzureDiscoverySettings _settings;
        private readonly ClusterMemberTableClient _client;
        private readonly string _host;
        private readonly IPAddress _address;
        private readonly int _port;
        private readonly CancellationTokenSource _shutdownCts;

        public AzureDiscoveryGuardian(AzureDiscoverySettings settings)
        {
            _settings = settings;
            _log = Logging.GetLogger(Context.System, nameof(AzureDiscoveryGuardian));
            _client = new ClusterMemberTableClient(
                serviceName: _settings.ServiceName,
                connectionString: _settings.ConnectionString,
                tableName: _settings.TableName,
                log: _log);

            var management = AkkaManagement.Get(Context.System);
            
            // Can management host be parsed as an IP?
            if (IPAddress.TryParse(management.Settings.Http.Hostname, out var ip))
            {
                _address = ip;
                _host = Dns.GetHostName();
            }
            else
            {
                // If its not an IP address, then its most probably a host name
                _host = management.Settings.Http.Hostname;
                var addresses = Dns.GetHostAddresses(_host);
                _address = addresses
                    .First(i => 
                        !Equals(i, IPAddress.Any) && 
                        !Equals(i, IPAddress.Loopback) && 
                        !Equals(i, IPAddress.IPv6Any) &&
                        !Equals(i, IPAddress.IPv6Loopback));
            }
            
            _port = management.Settings.Http.Port;
            _shutdownCts = new CancellationTokenSource();
            
            Become(Initializing);
        }

        protected override void PreStart()
        {
            if(_log.IsDebugEnabled)
                _log.Debug("Actor started");
            
            base.PreStart();
            _client.GetOrCreateAsync(_host, _address, _port, _shutdownCts.Token)
                .AsTask().PipeTo(Self, success: _ => Done.Instance);
        }

        protected override void PostStop()
        {
            base.PostStop();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
            
            if(_log.IsDebugEnabled)
                _log.Debug("Actor stopped");
        }

        private bool Initializing(object message)
        {
            if (message is Done)
            {
                Context.System.ActorOf(HeartbeatActor.Props(_settings, _client));
                Context.System.ActorOf(PruneActor.Props(_settings, _client));
                
                Become(Running);
                Stash.UnstashAll();
                
                if(_log.IsDebugEnabled)
                    _log.Debug("Actor initialized");
            }
            else
            {
                Stash.Stash();
            }

            return true;
        }

        private bool Running(object message)
        {
            switch (message)
            {
                case Lookup lookup:
                    if (lookup.ServiceName != _settings.ServiceName)
                        throw new Exception(
                            $"Lookup ServiceName mismatch. Expected: {_settings.ServiceName}, received: {lookup.ServiceName}");
                    
                    if(_log.IsDebugEnabled)
                        _log.Debug("Lookup started for service {0}", lookup.ServiceName);
                    
                    _client.GetAllAsync((DateTime.UtcNow - _settings.StaleTtlThreshold).Ticks, _shutdownCts.Token)
                        .PipeTo(Sender);
                    
                    return true;
                
                default:
                    return false;
            }
        }
        
        protected override void OnReceive(object message)
        {
            throw new NotImplementedException();
        }

        public IStash Stash { get; set; }
    }
}
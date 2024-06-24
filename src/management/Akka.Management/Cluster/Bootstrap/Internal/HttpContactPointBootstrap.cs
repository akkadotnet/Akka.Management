//-----------------------------------------------------------------------
// <copyright file="HttpContactPointBootstrap.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Management.Cluster.Bootstrap.ContactPoint;
using Akka.Management.Cluster.Bootstrap.Util;
using Akka.Pattern;
using Akka.Util;
using Newtonsoft.Json;
using static Akka.Discovery.ServiceDiscovery;
using static Akka.Management.Cluster.Bootstrap.Internal.BootstrapCoordinator.Protocol;

namespace Akka.Management.Cluster.Bootstrap.Internal
{
    internal class HttpContactPointBootstrap : ReceiveActor, IWithTimers
    {
        public static Props Props(ClusterBootstrapSettings settings, ResolvedTarget contactPoint, Uri baseUri)
            => Actor.Props.Create(() => new HttpContactPointBootstrap(settings, contactPoint, baseUri));
        
        public static string Name(string host, int port)
        {
            const string validSymbols = "-_.*$+:@&=,!~';";
            var cleanHost = host.Where(c =>
                (c >= 'a' && c <= 'z') || 
                (c >= 'A' && c <= 'Z') || 
                (c >= '0' && c <= '9') ||
                validSymbols.Contains(c)).ToArray();
            
            var sb = new StringBuilder("contactPointProbe-")
                .Append(cleanHost)
                .Append("-")
                .Append(port.ToString());
            
            return sb.ToString();
        }
        
        private sealed class ProbeTick : IDeadLetterSuppression
        {
            public static readonly ProbeTick Instance = new ProbeTick();
            private ProbeTick() { }
        }

        private const string ProbingTimerKey = "probing-key";

        private readonly ClusterBootstrapSettings _settings;
        private readonly ResolvedTarget _contactPoint;
        private readonly Uri _baseUri;

        private readonly ILoggingAdapter _log;
        private readonly HttpClient _http;
        private readonly Akka.Cluster.Cluster _cluster;
        private readonly TimeSpan _probeInterval;
        private readonly string _probeRequest;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private DateTimeOffset _probingKeepFailingDeadline;
        private readonly TimeSpan _probingFailureTimeout;
        private bool _stopped = false;

        private void ResetProbingKeepFailingWithinDeadline()
            => _probingKeepFailingDeadline = DateTimeOffset.Now + _probingFailureTimeout;

        public HttpContactPointBootstrap(ClusterBootstrapSettings settings, ResolvedTarget contactPoint, Uri baseUri)
        {
            _cluster = Akka.Cluster.Cluster.Get(Context.System);
            
            if (baseUri.Host == (_cluster.SelfAddress.Host ?? "---") &&
                baseUri.Port == (_cluster.SelfAddress.Port ?? -1))
                throw new ArgumentException(
                    "Requested base Uri to be probed matches local remoting address, bailing out! " +
                    $"Uri: $baseUri, this node's remoting address: {_cluster.SelfAddress}");
            
            _settings = settings;
            _contactPoint = contactPoint;
            _baseUri = baseUri;

            _log = Context.GetLogger();
            _http = new HttpClient();
            _http.Timeout = _settings.ContactPoint.ProbingFailureTimeout;
            
            _probeInterval = settings.ContactPoint.ProbeInterval;
            _probeRequest = ClusterBootstrapRequests.BootstrapSeedNodes(baseUri);
            _cancellationTokenSource = new CancellationTokenSource();

            _probingFailureTimeout = _probeInterval + _settings.ContactPoint.ProbingFailureTimeout;
            
            ResetProbingKeepFailingWithinDeadline();

            Receive<ProbeTick>(_ =>
            {
                _log.Debug("Probing [{0}] for seed nodes...", _probeRequest);
                var self = Self;

                var getTask = _http.GetAsync(_probeRequest, _cancellationTokenSource.Token);
                getTask.ContinueWith(task =>
                {
                    if (_stopped) return (Status) new Status.Failure(new TaskCanceledException("Actor already stopped."));

                    if (task.IsCanceled)
                    {
                        return new Status.Failure(new TimeoutException($"Probing timeout of [{_baseUri}]"));
                    }

                    if (task.Exception != null)
                    {
                        foreach (var e in task.Exception.Flatten().InnerExceptions)
                        {
                            switch (e)
                            {
                                case TaskCanceledException _:
                                    return new Status.Failure(new TimeoutException($"Probing timeout of [{_baseUri}]"));
                                case SocketException se:
                                    if (se.SocketErrorCode == SocketError.ConnectionRefused)
                                    {
                                        return new Status.Failure(se);
                                    }
                                    break;
                            }
                        }
                        return new Status.Failure(task.Exception);
                    }
                    
                    var response = task.Result;
                    var bodyTask = response.Content.ReadAsStringAsync();
                    bodyTask.Wait();
                    var body = bodyTask.Result;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var nodes = JsonConvert.DeserializeObject<HttpBootstrapJsonProtocol.SeedNodes>(body);
                        if(nodes?.SelfNode == null)
                            return new Status.Failure(new IllegalStateException(
                                $"Failed to deserialize HTTP response, Self node address is empty. [{(int) response.StatusCode} {response.StatusCode}]. Body: '{body}'"));
                        return new Status.Success(nodes);
                    }
                    return new Status.Failure(new IllegalStateException(
                        $"Expected response '200 OK' but found [{(int) response.StatusCode} {response.StatusCode}]. Body: '{body}'"));
                    
                }, TaskContinuationOptions.ExecuteSynchronously).PipeTo(self);
            });

            Receive<Status.Failure>(fail =>
            {
                if (_stopped) return;
                
                var cause = fail.Cause;
                _log.Warning(cause, "Probing [{0}] failed due to: {1}", _probeRequest, cause.Message);
                if (_probingKeepFailingDeadline.IsOverdue())
                {
                    _log.Error("Overdue of probing-failure-timeout, stop probing, signaling that it's failed");
                    Context.Parent.Tell(new ProbingFailed(_contactPoint, cause));
                    Context.Stop(Self);
                }
                else
                {
                    // keep probing, hoping the request will eventually succeed
                    if(!_stopped)
                        ScheduleNextContactPointProbing();
                }
            });

            Receive<Status.Success>(success =>
            {
                if (_stopped) return;
                
                var nodes = (HttpBootstrapJsonProtocol.SeedNodes) success.Status;
                NotifyParentAboutSeedNodes(nodes);
                ResetProbingKeepFailingWithinDeadline();
                // we keep probing and looking if maybe a cluster does form after all
                // (technically could be long polling or web-sockets, but that would need reconnect logic, so this is simpler)
                ScheduleNextContactPointProbing();
            });
        }

        public ITimerScheduler? Timers { get; set; }
        private TimeSpan EffectiveProbeInterval => _probeInterval + Jitter(_probeInterval);

        protected override void PreStart()
        {
            Self.Tell(ProbeTick.Instance);
        }

        protected override void PostStop()
        {
            _stopped = true;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            Timers!.CancelAll();
            base.PostStop();
        }

        private void NotifyParentAboutSeedNodes(HttpBootstrapJsonProtocol.SeedNodes members)
        {
            var seedAddresses = members.Nodes.Select(n => n.Node).ToImmutableHashSet();
            Context.Parent.Tell(new ObtainedSeedNodesObservation(
                DateTimeOffset.Now, 
                _contactPoint,
                members.SelfNode,
                seedAddresses));
        }

        private void ScheduleNextContactPointProbing()
        {
            Timers!.StartSingleTimer(ProbingTimerKey, ProbeTick.Instance, EffectiveProbeInterval);
        }

        private TimeSpan Jitter(TimeSpan d)
        {
            var ticks = d.Ticks * _settings.ContactPoint.ProbeIntervalJitter * ThreadLocalRandom.Current.NextDouble();
            return new TimeSpan((long) ticks);
        }
    }
}
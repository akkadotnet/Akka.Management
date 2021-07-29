using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
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
    internal class ContactPointBootstrap : ReceiveActor, IWithTimers
    {
        public static Props Props(ClusterBootstrapSettings settings, ResolvedTarget contactPoint, Uri baseUri)
            => Actor.Props.Create(() => new ContactPointBootstrap(settings, contactPoint, baseUri));
        
        public static string Name(string host, int port)
        {
            const string validSymbols = "-_.*$+:@&=,!~';";
            var cleanHost = host.Where(c =>
                (c >= 'a' && c <= 'z') || 
                (c >= 'A' && c <= 'Z') || 
                (c >= '0' && c <= '9') ||
                validSymbols.Contains(c));
            
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
        private readonly Uri _probeRequest;
        private readonly TimeSpan _replyTimeout;
        
        private DateTimeOffset _probingKeepFailingDeadline;
        private CancellationTokenSource _currentCancellationTokenSource;

        private void ResetProbingKeepFailingWithinDeadline()
            => _probingKeepFailingDeadline = DateTimeOffset.Now + _settings.ContactPoint.ProbingFailureTimeout;

        public ContactPointBootstrap(ClusterBootstrapSettings settings, ResolvedTarget contactPoint, Uri baseUri)
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
            _probeInterval = settings.ContactPoint.ProbeInterval;
            _probeRequest = ClusterBootstrapRequests.BootstrapSeedNodes(baseUri);
            
            ResetProbingKeepFailingWithinDeadline();

            Receive<ProbeTick>(_ =>
            {
                _log.Debug("Probing [{0}] for seed nodes...", _probeRequest);
                _currentCancellationTokenSource?.Dispose();
                _currentCancellationTokenSource = new CancellationTokenSource(_settings.ContactPoint.ProbingFailureTimeout); 
                _http.GetAsync(_probeRequest, _currentCancellationTokenSource.Token).ContinueWith(async task =>
                {
                    if (task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
                    {
                        await HandleResponse(task.Result);
                        return;
                    }

                    if (task.IsCanceled)
                    {
                        Self.Tell(new Status.Failure(new TimeoutException($"Probing timeout of [{_baseUri}]")));
                        return;
                    }
                    
                    Self.Tell(new Status.Failure(task.Exception));
                });
            });

            Receive<Status.Failure>(fail =>
            {
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
                    ScheduleNextContactPointProbing();
                }
            });

            Receive<BootstrapProtocol.SeedNodes>(response =>
            {
                NotifyParentAboutSeedNodes(response);
                ResetProbingKeepFailingWithinDeadline();
                // we keep probing and looking if maybe a cluster does form after all
                // (technically could be long polling or web-sockets, but that would need reconnect logic, so this is simpler)
                ScheduleNextContactPointProbing();
            });
        }

        public ITimerScheduler Timers { get; set; }
        private TimeSpan EffectiveProbeInterval => _probeInterval + Jitter(_probeInterval);

        protected override void PostStop()
        {
            base.PostStop();
            if (_currentCancellationTokenSource != null)
            {
                _currentCancellationTokenSource.Cancel();
                _currentCancellationTokenSource.Dispose();
            }
        }

        private async Task HandleResponse(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Self.Tell(JsonConvert.DeserializeObject<BootstrapProtocol.SeedNodes>(body)); 
            }
            else
            {
                Self.Tell(new Status.Failure(new IllegalStateException(
                    $"Expected response '200 OK' but found {response.StatusCode}. Body: '{body}'")));
            }
        }
        
        private void NotifyParentAboutSeedNodes(BootstrapProtocol.SeedNodes members)
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
            Timers.StartSingleTimer(ProbingTimerKey, ProbeTick.Instance, EffectiveProbeInterval);
        }

        private TimeSpan Jitter(TimeSpan d)
        {
            var ticks = d.Ticks * _settings.ContactPoint.ProbeIntervalJitter * ThreadLocalRandom.Current.NextDouble();
            return new TimeSpan((long) ticks);
        }
    }
}
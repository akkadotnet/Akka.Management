//-----------------------------------------------------------------------
// <copyright file="SelfAwareJoinDecider.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;
using Akka.Event;

namespace Akka.Management.Cluster.Bootstrap
{
    public abstract class SelfAwareJoinDecider : IJoinDecider
    {
        private readonly ActorSystem _system;
        
        protected ILoggingAdapter Log { get; }
        
        protected SelfAwareJoinDecider(ActorSystem system, ClusterBootstrapSettings settings)
        {
            Settings = settings;
            _system = system;
            Log = Logging.GetLogger(_system, typeof(SelfAwareJoinDecider));
        }
        
        public ClusterBootstrapSettings Settings { get; } 

        protected string ContactPointString((string host, int port) contactPoint)
            => $"{contactPoint.host}:{contactPoint.port}";

        protected string ContactPointString(ServiceDiscovery.ResolvedTarget contactPoint)
            => $"{contactPoint.Host}:{(contactPoint.Port ?? 0)}";

        internal (string host, int port) SelfContactPoint()
        {
            var task = ClusterBootstrap.Get(_system).SelfContactPoint;
            task.Wait();
            return (task.Result.Host, task.Result.Port);
        }

        public bool CanJoinSelf(ServiceDiscovery.ResolvedTarget target, SeedNodesInformation info)
        {
            var self = SelfContactPoint();
            if (MatchesSelf(target, self))
                return true;
            
            if (!info.ContactPoints.Any(t => MatchesSelf(t, self)))
            {
                Log.Warning(
                    "Self contact point [{0}] (hostname: {1}, port: {2}) not found in targets [{3}]",
                    ContactPointString(self),
                    self.host,
                    self.port,
                    string.Join(", ", info.ContactPoints.Select(ContactPointString)));
            }
            return false;
        }

        public bool MatchesSelf(ServiceDiscovery.ResolvedTarget target, (string host, int port) contactPoint)
        {
            if (target.Port == null)
                return HostMatches(contactPoint.host, target);

            var hostMatches = HostMatches(contactPoint.host, target);
            var portMatches = contactPoint.port == target.Port;
            var result = hostMatches && portMatches;

            if (Log.IsDebugEnabled && hostMatches && !portMatches)
            {
                Log.Debug(
                    "MatchesSelf: Hostname matched but port mismatch - self port={0}, target port={1}",
                    contactPoint.port, target.Port);
            }

            return result;
        }

        public bool HostMatches(string host, ServiceDiscovery.ResolvedTarget target)
        {
            var cleaned = _hostReplaceRegex.Replace(host, "");
            var directMatch = string.Equals(host, target.Host, StringComparison.OrdinalIgnoreCase);
            var addressString = target.Address?.ToString() ?? "";
            var containsMatch = addressString.Contains(cleaned);
            var result = directMatch || containsMatch;

            if (Log.IsDebugEnabled)
            {
                Log.Debug(
                    "HostMatches comparison: self hostname='{0}', target hostname='{1}', target address='{2}', " +
                    "direct match={3}, contains match={4}, result={5}",
                    host, target.Host, addressString, directMatch, containsMatch, result);
            }

            return result;
        }

        public abstract Task<IJoinDecision> Decide(SeedNodesInformation info);

        private readonly Regex _hostReplaceRegex = new Regex("[\\[\\]]");
    }
}
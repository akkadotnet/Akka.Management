//-----------------------------------------------------------------------
// <copyright file="MockDiscovery.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;
using Akka.Event;
using Akka.Util;

namespace Akka.Management.Tests.Cluster.Bootstrap
{
    public class MockDiscovery : ServiceDiscovery
    {
        private static readonly AtomicReference<ImmutableDictionary<Lookup, Func<ActorSystem, Task<Resolved>>>>
            Data = new AtomicReference<ImmutableDictionary<Lookup, Func<ActorSystem, Task<Resolved>>>>(ImmutableDictionary<Lookup, Func<ActorSystem, Task<Resolved>>>.Empty);

        public static void Set(Lookup name, Func<ActorSystem, Task<Resolved>> to)
        {
            while (true)
            {
                var d = Data.Value;
                if (Data.CompareAndSet(d, d.SetItem(name, to)))
                    break;
            }
        }

        public static void Set(Lookup name, Func<Task<Resolved>> to)
            => Set(name, _ => to());

        public static void Remove(Lookup name)
        {
            while (true)
            {
                var d = Data.Value;
                if (Data.CompareAndSet(d, d.Remove(name)))
                    break;
            }
        }

        private readonly ActorSystem _system;
        private readonly ILoggingAdapter _log;

        public MockDiscovery(ActorSystem system)
        {
            _system = system;
            _log = Logging.GetLogger(system, GetType());
        }

        public override Task<Resolved> Lookup(Lookup query, TimeSpan resolveTimeout)
        {
            if (Data.Value.TryGetValue(query, out var res))
            {
                var items = res(_system);
                _log.Info("Mock-resolved [{0}] to [{1}:{2}]", query, items, items.Result);
                return items;
            }

            _log.Info(
                "No mock-data for [{0}], resolving as 'null'. Current mocks: {1}", 
                query, 
                string.Join(", ", Data.Value.Select(kvp => $"{kvp.Key.Protocol} {kvp.Key.ServiceName} {kvp.Key.PortName}"))) ;
            return Task.FromResult(new Resolved(query.ServiceName, null));
        }
    }
}
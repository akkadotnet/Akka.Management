//-----------------------------------------------------------------------
// <copyright file="HealthChecks.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Util;

namespace Akka.Management.Dsl
{
    internal abstract class HealthChecks
    {
        public abstract Task<bool> Ready();
        public abstract Task<Either<string, Done>> ReadyResult();
        public abstract Task<bool> Alive();
        public abstract Task<Either<string, Done>> AliveResult();
    }

    public sealed class ReadinessCheckSetup : Setup
    {
        public ReadinessCheckSetup(Func<ActorSystem, ImmutableList<IHealthCheck>> createHealthChecks)
        {
            CreateHealthChecks = createHealthChecks;
        }

        public Func<ActorSystem, ImmutableList<IHealthCheck>> CreateHealthChecks { get; }
    }

    public sealed class LivenessCheckSetup : Setup
    {
        public LivenessCheckSetup(Func<ActorSystem, ImmutableList<IHealthCheck>> createHealthChecks)
        {
            CreateHealthChecks = createHealthChecks;
        }

        public Func<ActorSystem, ImmutableList<IHealthCheck>> CreateHealthChecks { get; }
    }
}
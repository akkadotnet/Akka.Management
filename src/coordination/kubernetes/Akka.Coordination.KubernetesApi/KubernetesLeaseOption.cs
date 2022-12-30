// -----------------------------------------------------------------------
//  <copyright file="KubernetesLeaseOption.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor.Setup;
using Akka.Cluster.Hosting.SBR;
using Akka.Hosting;
using Akka.Hosting.Coordination;

#nullable enable
namespace Akka.Coordination.KubernetesApi
{
    public class KubernetesLeaseOption: LeaseOptionBase
    {
        public static readonly KubernetesLeaseOption Instance = new KubernetesLeaseOption();

        private KubernetesLeaseOption()
        {
        }
        
        public override string ConfigPath { get; } = "akka.coordination.lease.kubernetes";
        public override Type Class { get; } = typeof(KubernetesLease);
        
        public override void Apply(AkkaConfigurationBuilder builder, Setup? setup = null)
        {
            throw new NotImplementedException("Not intended to be applied, use the `WithKubernetesLease()` Akka.Hosting extension method instead.");
        }
    }
}
// -----------------------------------------------------------------------
//  <copyright file="AzureLeaseOption.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor.Setup;
using Akka.Cluster.Hosting.SBR;
using Akka.Hosting;
using Akka.Hosting.Coordination;

namespace Akka.Coordination.Azure
{
    public class AzureLeaseOption: LeaseOptionBase
    {
        public static readonly AzureLeaseOption Instance = new ();

        private AzureLeaseOption()
        {
        }
        
        public override string ConfigPath { get; } = "akka.coordination.lease.azure";
        public override Type Class { get; } = typeof(AzureLease);
        
        public override void Apply(AkkaConfigurationBuilder builder, Setup? setup = null)
        {
            throw new NotImplementedException("Not intended to be applied, use the `WithAzureLease()` Akka.Hosting extension method instead.");
        }
    }
}
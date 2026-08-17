// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("akka-discovery");

var akka = builder.AddAkka("sample-cluster")
    .WithClustering(redis);

builder.AddProject<Projects.Akka_Aspire_Sample_Service>("service")
    .WithHttpEndpoint(name: "http")
    .WithReplicas(3)
    .WithReference(akka);

builder.Build().Run();

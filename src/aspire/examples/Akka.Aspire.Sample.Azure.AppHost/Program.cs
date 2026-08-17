// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("azure-storage").RunAsEmulator();
var tables = storage.AddTables("akka-discovery");

var akka = builder.AddAkka("sample-cluster")
    .WithClustering(tables);

builder.AddProject<Projects.Akka_Aspire_Sample_Azure_Service>("service")
    .WithHttpEndpoint(name: "http")
    .WithReplicas(3)
    .WithReference(akka);

builder.Build().Run();

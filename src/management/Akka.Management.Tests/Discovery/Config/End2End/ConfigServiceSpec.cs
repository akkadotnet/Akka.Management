// -----------------------------------------------------------------------
//  <copyright file="ConfigServiceSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Discovery.Config.Hosting;
using Akka.Hosting;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using Akka.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Management.Tests.Discovery.Config.End2End;

public class ConfigServiceSpec: Akka.Hosting.TestKit.TestKit
{
    private const int ClusterNodeCount = 3;
    
    private readonly AtomicBoolean _clusterFormed = new ();
    private readonly int[] _remotingPorts = new int[ClusterNodeCount];
    private readonly int[] _managementPorts = new int[ClusterNodeCount];
    private readonly string[] _managementEndpoints = new string[ClusterNodeCount];

    private IHost? _host1;
    private IHost? _host2;
    private ActorSystem? _sys1;
    private ActorSystem? _sys2;

    public ConfigServiceSpec(ITestOutputHelper output) : base(nameof(ConfigServiceSpec), output)
    {
        var rnd = new Random();
        var port = rnd.Next(30000, 40000);
        for (var i = 0; i < ClusterNodeCount; i++)
        {
            _remotingPorts[i] = port;
            port++;
            _managementPorts[i] = port;
            _managementEndpoints[i] = $"127.0.0.1:{port}";
            port++;
        }
    }

    #region Test Setup

    private async Task<IHost> StartAkkaHost(int index)
    {
        var hostBuilder = new HostBuilder();
        hostBuilder
            .ConfigureLogging(logger =>
            {
                logger.ClearProviders();
                logger.AddProvider(new XUnitLoggerProvider(Output!, LogLevel));
                logger.AddFilter("Akka.*", LogLevel);
            })
            .ConfigureServices((_, services) =>
            {
                services.AddAkka(nameof(ConfigServiceSpec), (builder, _) =>
                {
                    AddConfigDiscovery(builder, index);
                });
            });
        var host = hostBuilder.Build();
        await host.StartAsync();

        return host;
    }
    
    private AkkaConfigurationBuilder AddConfigDiscovery(AkkaConfigurationBuilder builder, int index)
    {
        var port = _remotingPorts[index];
        var managementPort = _managementPorts[index];
        
        return builder
            .WithRemoting(options =>
            {
                options.Port = port;
                options.HostName = "localhost";
            })
            .WithClustering(new ClusterOptions
            {
                MinimumNumberOfMembers = 3
            })
            .WithClusterBootstrap(
                requiredContactPoints:3,
                serviceName:"LocalService",
                // NOTE: this is needed to prevent cluster bootstrap from filtering out multiple result from a single domain name 
                portName:"port")
            .WithAkkaManagement(
                hostName:"127.0.0.1",
                port: managementPort,
                bindHostname:"127.0.0.1",
                bindPort:managementPort,
                autoStart:true)
            .WithConfigDiscovery(opt =>
            {
                opt.Services.Add(new Service
                {
                    Name = "LocalService",
                    Endpoints = _managementEndpoints
                });
            })
            .WithConfigDiscovery(new ConfigServiceDiscoveryOptions
            {
                Services = new List<Service>
                {
                    new Service
                    {
                        Name = "LocalService",
                        Endpoints = _managementEndpoints
                    }
                }
            });
    }
    
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        AddConfigDiscovery(builder, 0)
            .AddStartup((system, _) =>
            {
                var cluster = Akka.Cluster.Cluster.Get(system);
                cluster.RegisterOnMemberUp(() =>
                {
                    _clusterFormed.CompareAndSet(false, true);
                });
            });
    }
    
    protected override async Task BeforeTestStart()
    {
        await base.BeforeTestStart();
        _host1 = await StartAkkaHost(1);
        _sys1 = _host1.Services.GetRequiredService<ActorSystem>();
        _host2 = await StartAkkaHost(2);
        _sys2 = _host2.Services.GetRequiredService<ActorSystem>();
        Output!.WriteLine("Systems started");
    }

    protected override async Task AfterAllAsync()
    {
        await base.AfterAllAsync();
        
        var tasks = new List<Task>();
        if (_sys1 is not null)
            tasks.Add(_sys1.Terminate());
        if(_sys2 is not null)
            tasks.Add(_sys2.Terminate());
        await Task.WhenAll(tasks);
        
        _host1?.Dispose();
        _host2?.Dispose();
    }

    #endregion


    [Fact(DisplayName = "Cluster should form")]
    public async Task ClusterFormingSpec()
    {
        await AwaitConditionAsync(() => Task.FromResult(_clusterFormed.Value), max:TimeSpan.FromSeconds(30));
    }
}
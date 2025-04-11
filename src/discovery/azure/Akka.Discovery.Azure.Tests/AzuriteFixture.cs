using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Testcontainers.Azurite;
using Xunit;

namespace Akka.Discovery.Azure.Tests;

[CollectionDefinition(nameof(AzuriteSpecs))]
public class AzuriteSpecs : ICollectionFixture<AzuriteFixture>
{
    
}

public class AzuriteFixture : IAsyncLifetime
{
    public string ConnectionString
    {
        get
        {
            if (Container is null || Container.State != TestcontainersStates.Running)
                throw new Exception("Container has not been initialized");
            
            var blobPort = Container.GetMappedPublicPort(AzuriteBuilder.BlobPort);
            var queuePort = Container.GetMappedPublicPort(AzuriteBuilder.QueuePort);
            var tablePort = Container.GetMappedPublicPort(AzuriteBuilder.TablePort);
            return "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
                   "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
                   $"BlobEndpoint=http://127.0.0.1:{blobPort}/devstoreaccount1;" +
                   $"QueueEndpoint=http://127.0.0.1:{queuePort}/devstoreaccount1;" +
                   $"TableEndpoint=http://127.0.0.1:{tablePort}/devstoreaccount1;";
        }
    }
    
    public async Task InitializeAsync()
    {
        var builder = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithName($"azurite-{Guid.NewGuid()}");
        Container = builder.Build();
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
            await Container.StopAsync();
    }

    public DockerContainer? Container { get; protected set; }
}
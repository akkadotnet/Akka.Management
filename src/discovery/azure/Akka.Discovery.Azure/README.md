# Azure Table Storage Based Discovery

This module can be used as a discovery method for any cluster that has access to an Azure Table Storage service.

To use `Akka.Discovery.Azure` in your project, you must also include `Akka.Discovery` in your project nuget package dependency.

## Configuring Using HOCON

You will need to include these HOCON settings in your HOCON configuration:
```
akka.discovery {
  method = azure
  azure {
    # The service name assigned to the cluster.
    service-name = "default"
    
    # The connection string used to connect to Azure Table hosting the cluster membership table
    # MANDATORY FIELD: MUST be provided, else the discovery plugin WILL throw an exception.
    connection-string = "<connection-string>"
  }
}
```

__Notes__
* The `akka.discovery.azure.connection-string` setting is mandatory
* For `Akka.Discovery.Azure` to work with multiple clusters, each cluster will have to have different `akka.discovery.azure.service-name` settings.

## Configuring Using ActorSystemSetup

You can programmatically configure `Akka.Discovery.Azure` using the `AzureDiscoverySetup` class.

```C#
var config = ConfigurationFactory.ParseString(File.ReadAllText("app.conf"));

var bootstrap = BootstrapSetup.Create()
    .WithConfig(config) // load HOCON
    .WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster
                
var azureSetup = new AzureDiscoverySetup()
    .WithConnectionString(connectionString);

var actorSystemSetup = bootstrap.And(azureSetup);

var system = ActorSystem.Create("my-system", actorSystemSetup);
```

## Using Discovery Together with Akka.Management and Cluster.Bootstrap
All discovery plugins are designed to work with Cluster.Bootstrap to provide an automated way to form a cluster that is not based on hard wired seeds configuration. Some HOCON configuration is needed to make discovery work with Cluster.Bootstrap:

```
akka.discovery.method = azure
akka.discovery.azure.connection-string = "UseDevelopmentStorage=true"
akka.management.http.routes = {
    cluster-bootstrap = "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap"
}
```

You then start the cluster bootstrapping process by calling:
```C#
await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();
```

A more complete example:
```C#
var config = ConfigurationFactory
    .ParseString(File.ReadAllText("app.conf""))
    .WithFallback(ClusterBootstrap.DefaultConfiguration())
    .WithFallback(AkkaManagementProvider.DefaultConfiguration());

var bootstrap = BootstrapSetup.Create()
    .WithConfig(config) // load HOCON
    .WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster

var azureSetup = new AzureDiscoverySetup()
    .WithConnectionString(connectionString);

var actorSystemSetup = bootstrap.And(azureSetup);

var system = ActorSystem.Create("my-system", actorSystemSetup);

var log = Logging.GetLogger(system, this);

await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();

var cluster = Cluster.Get(system);
cluster.RegisterOnMemberUp(() => {
  var upMembers = cluster.State.Members
      .Where(m => m.Status == MemberStatus.Up)
      .Select(m => m.Address.ToString());

  log.Info($"Current up members: [{string.Join(", ", upMembers)}]")
});
```

# Akka Management
Akka Management is the core module of the management utilities which provides a central HTTP endpoint for Akka management extensions.

## Basic Usage
With a few exceptions, Akka Management does not start automatically and the routes will only be exposed once you trigger:
```
AkkaManagement.Get(system).Start();
```

This allows users to prepare anything further before exposing routes for the bootstrap joining process and other purposes.
Please note that once it is started, you can not add or expose more routes on the HTTP endpoint.

## Basic Configuration
You can configure hostname and port to use for the HTTP Cluster management by overriding the following:
```
akka.management.http.hostname = "127.0.0.1"
akka.management.http.port = 8558
```
Note that the default value for hostname is `localhost`

When running Akka nodes behind NATs or inside docker containers in bridge mode, it is necessary to set different hostname and port 
number to bind for the HTTP Server for Http Cluster Management:
```
  akka.management.http.hostname = "my-public-host-name"
  akka.management.http.port = 8558
  # Bind to 0.0.0.0:8558 'internally': 
  akka.management.http.bind-hostname = 0.0.0.0
  akka.management.http.bind-port = 8558
```

## Exposed REST API Endpoints
Two Akka.Management REST API endpoints are exposed by default, the health check endpoint at 
`http://{host}:{port}/health` and readiness check endpoint at `http://{host}:{port}/alive`; endpoint
paths are configurable inside the HOCON configuration. Both endpoints will return a 200-OK if all of 
its callbacks returns a `Done` instance. If any of the callbacks returns a string, the endpoint will 
return a 500-Internal Server Error code and includes the string reason message inside the HTTP body.

## Security

Note that http protocol is used by default and, as of now, there is no way to set up security on any HTTP endpoints. Management endpoints
are not designed to be and should never be opened to the public.

## Stopping Akka Management
In a dynamic environment you might stop instances of Akka Management, for example if you want to free up resources taken by 
the HTTP server serving the Management routes.

You can do so by calling Stop() on AkkaManagement. This method return a Task to inform when the server has been stopped.

```C#
var management = AkkaManagement.Get(system);
await management.start();
//...
await management.stop();
```

## Reference HOCON Configuration
```
######################################################
# Akka Http Cluster Management Reference Config File #
######################################################

# This is the reference config file that contains all the default settings.
# Make your edits/overrides in your application.conf.

akka.management {
  http {
    # The hostname where the HTTP Server for Http Cluster Management will be started.
    # This defines the interface to use.
    # InetAddress.getLocalHost.getHostAddress is used not overriden or empty
    hostname = "<hostname>"

    # The port where the HTTP Server for Http Cluster Management will be bound.
    # The value will need to be from 0 to 65535.
    port = 8558 # port pun, it "complements" 2552 which is often used for Akka remoting

    # Use this setting to bind a network interface to a different hostname or ip
    # than the HTTP Server for Http Cluster Management.
    # Use "0.0.0.0" to bind to all interfaces.
    # akka.management.http.hostname if empty
    bind-hostname = ""

    # Use this setting to bind a network interface to a different port
    # than the HTTP Server for Http Cluster Management. This may be used
    # when running akka nodes in a separated networks (under NATs or docker containers).
    # Use 0 if you want a random available port.
    #
    # akka.management.http.port if empty
    bind-port = ""

    # Definition of management route providers which shall contribute routes to the management HTTP endpoint.
    # Management route providers should be regular extensions that aditionally extend the
    # `akka.management.scaladsl.ManagementRoutesProvider` or
    # `akka.management.javadsl.ManagementRoutesProvider` interface.
    #
    # Libraries may register routes into the management routes by defining entries to this setting
    # the library `reference.conf`:
    #
    # akka.management.http.routes {
    #   name = "FQCN"
    # }
    #
    # Where the `name` of the entry should be unique to allow different route providers to be registered
    # by different libraries and applications.
    #
    # The FQCN is the fully qualified class name of the `ManagementRoutesProvider`.
    #
    # By default the `akka.management.HealthCheckRoutes` is enabled, see `health-checks` section of how
    # configure specific readiness and liveness checks.
    #
    # Route providers included by a library (from reference.conf) can be excluded by an application
    # by using "" or null as the FQCN of the named entry, for example:
    #
    # akka.management.http.routes {
    #   health-checks = ""
    # }
    routes {
        health-checks = "Akka.Management.HealthCheckRoutes, Akka.Management"
    }

    # Should Management route providers only expose read only endpoints? It is up to each route provider
    # to adhere to this property
    route-providers-read-only = true
  }
  
  # Health checks for readiness and liveness
  health-checks {
    # When exposting health checks via Akka Management, the path to expose readiness checks on
    readiness-path = "/ready"
    # When exposting health checks via Akka Management, the path to expose readiness checks on
    liveness-path = "/alive"
    # All readiness checks are executed in parallel and given this long before the check is timed out
    check-timeout = 1s
    # Add readiness and liveness checks to the below config objects with the syntax:
    #
    # name = "FQCN"
    #
    # For example:
    #
    # cluster-membership = "akka.management.cluster.scaladsl.ClusterMembershipCheck"
    #
    # Libraries and frameworks that contribute checks are expected to add their own checks to their reference.conf.
    # Applications can add their own checks to application.conf.
    readiness-checks {
      
    }
    liveness-checks {
      
    }
  }
}
```
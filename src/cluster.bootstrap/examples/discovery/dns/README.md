# DNS Cluster Bootstrap Example

This example demonstrates how to use DNS-based service discovery with Akka.Management Cluster Bootstrap to form an Akka.NET Cluster.

Three types of records are supported: A, AAAA and SRV.

To run exmample use:
```pwsh
./build.ps1 [a|aaaa|srv] 
```
This will build example on your host machine and spawn dedicated docker-compose file for each record type.
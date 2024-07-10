# Akka.Management Cluster Bootstrap Setup Using Pure HOCON Configuration

This sample project shows how Akka.Management cluster bootstrapping can be achieved using pure HOCON configuration, no Akka.Hosting is used in this project.

## Running Sample

To run the sample, you must have Docker Desktop installed on your machine.

1. Open a terminal window and go to the sample directory 
   ```powershell
   PS C:\> cd ./Akka.Management/src/cluster.bootstrap/examples/discovery/hocon-kubernetes
   ```
2. Start by building the Docker image
   ```powershell
   PS C:\> ./build-docker.ps1
   ```
3. Deploy the Kubernetes cluster
   ```powershell
   PS C:\> ./k8s/deploy.cmd
   ```

There are several scripts in the `.\k8s` directory:

* `deploy.cmd`: Deploys the Kubernetes cluster
* `destroy.cmd`: Take down the Kubernetes cluster
* `events.cmd`: List all the events that happens inside the Kubernetes cluster
* `status.cmd`: Show the status of the Kubernetes cluster
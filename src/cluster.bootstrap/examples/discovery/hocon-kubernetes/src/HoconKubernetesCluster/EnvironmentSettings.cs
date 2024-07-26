using System.Text;
using Akka.Configuration;

namespace HoconKubernetesCluster;

public record EnvironmentSettings(
    string? RemotingHostName,
    string? RemotingPort,
    string? ManagementPort,
    string? BootstrapServiceName,
    string? BootstrapPortName)
{
    private const string RemoteHostNameKey = "AKKA_REMOTE_HOSTNAME";
    private const string RemotePortKey = "AKKA_REMOTE_PORT";
    private const string ManagementPortKey = "AKKA_MANAGEMENT_PORT";
    private const string BootstrapServiceNameKey = "AKKA_BOOTSTRAP_SERVICE_NAME";
    private const string BootstrapPortNameKey = "AKKA_BOOTSTRAP_PORT_NAME";

    public Config ToConfig()
    {
        var sb = new StringBuilder();
        if (RemotingHostName is not null)
            sb.AppendLine($"akka.remote.dot-netty.tcp.public-hostname = {RemotingHostName}");
        
        if(RemotingPort is not null)
            sb.AppendLine($"akka.remote.dot-netty.tcp.port = {RemotingPort}");

        if (ManagementPort is not null)
            sb.AppendLine($"akka.management.http.port = {ManagementPort}");

        if (BootstrapServiceName is not null)
            sb.AppendLine($"akka.management.cluster.bootstrap.contact-point-discovery.service-name = {BootstrapServiceName}");

        if (BootstrapPortName is not null)
            sb.AppendLine($"akka.management.cluster.bootstrap.contact-point-discovery.port-name = {BootstrapPortName}");

        return sb.Length == 0 ? Config.Empty : sb.ToString();
    }

    public static EnvironmentSettings Create()
        => new (
            RemotingHostName: Environment.GetEnvironmentVariable(RemoteHostNameKey),
            RemotingPort: Environment.GetEnvironmentVariable(RemotePortKey),
            ManagementPort: Environment.GetEnvironmentVariable(ManagementPortKey),
            BootstrapServiceName: Environment.GetEnvironmentVariable(BootstrapServiceNameKey),
            BootstrapPortName: Environment.GetEnvironmentVariable(BootstrapPortNameKey)
        );
}
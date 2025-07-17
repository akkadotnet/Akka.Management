using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor.Setup;
using Akka.Discovery.Dns.Internal;
using Akka.Hosting;

namespace Akka.Discovery.Dns;

/// <summary>
/// Options class for configuring the DNS service discovery.
/// </summary>
/// 
public class DnsDiscoveryOptions : IDiscoveryOptions
{
    /// <summary>
    /// Default configuration path for DNS service discovery
    /// </summary>
    public const string DefaultPath = "akka-dns";
    
    /// <summary>
    /// Gets the full configuration path for the specified path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The full configuration path.</returns>
    private static string FullPath(string path) => $"akka.discovery.{path}";

    /// <summary>
    /// Gets the type of service discovery class.
    /// </summary>
    public Type Class { get; } = typeof(DnsServiceDiscovery);

    /// <summary>
    /// Gets or sets the configuration path.
    /// </summary>
    public string ConfigPath { get; set; } = DefaultPath;

    /// <summary>
    /// Renders HOCON configuration based on current settings.
    /// </summary>
    /// <returns>HOCON configuration string.</returns>
    private string ToHocon()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath(ConfigPath)} {{");
        sb.AppendLine($"  class = \"{Class.FullName}, {Class.Assembly.GetName().Name}\"");
        sb.AppendLine("}");
            
        return sb.ToString();
    }
    /// <summary> </summary>
    /// <param name="builder"></param>
    /// <param name="setup"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void Apply(AkkaConfigurationBuilder builder, Setup? setup = null)
    {
        builder.AddHocon(ToHocon(), HoconAddMode.Prepend);
    }
}

/// <summary>
/// Setup class for configuring the DNS service discovery.
/// </summary>
public class DnsDiscoverySetup : Setup
{
    /// <summary>
    /// Gets or sets the discovery ID.
    /// </summary>
    public string DiscoveryId { get; set; } = DnsDiscoveryOptions.DefaultPath;
        
    // Other configuration options can be added here
        
    /// <summary>
    /// Applies the setup to the provided settings.
    /// </summary>
    /// <returns>The updated settings.</returns>
    internal DnsDiscoverySettings Apply(DnsDiscoverySettings settings)
    {
        return settings; // No custom settings yet
    }
}
    
/// <summary>
/// Settings class for the DNS service discovery.
/// </summary>
public class DnsDiscoverySettings 
{
    /// <summary>
    /// Gets an empty settings instance.
    /// </summary>
    public static readonly DnsDiscoverySettings Empty = new DnsDiscoverySettings();
        
    /// <summary>
    /// Creates settings from an Akka ActorSystem.
    /// </summary>
    /// <param name="system">The actor system.</param>
    /// <returns>The settings.</returns>
    public static DnsDiscoverySettings Create(Akka.Actor.ActorSystem system) 
        => Create(system.Settings.Config);
        
    /// <summary>
    /// Creates settings from configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The settings.</returns>
    public static DnsDiscoverySettings Create(Akka.Configuration.Config config)
    {
        return new DnsDiscoverySettings();
    }
}

public abstract record PositiveTtl
{
    
    public static PositiveTtl ParseFromConfig(Configuration.Config config) =>
        config.GetString("positive-ttl", "forever").ToLowerInvariant() switch
        {
            "forever" => Forever.Instance,
            "never" => Never.Instance,
            _ => new TtlTimeSpan(config.GetTimeSpan("positive-ttl")), 
        };
    public record Forever : PositiveTtl
    {
        public static readonly Forever Instance = new();
        public override string ToString() => "forever";
    }

    public record Never : PositiveTtl
    {
        public static readonly Never Instance = new();
        public override string ToString() => "never";
        
    }

    public record TtlTimeSpan(TimeSpan TimeSpan) : PositiveTtl
    {
        public override string ToString() => $"{TimeSpan.TotalSeconds}s";
    }
}

public class AsyncDnsResolverOptions : IHoconOption
{


    public const string DefaultPath = "async-dns";

    public const string NameserversPath = "nameservers";
    public List<string> Nameservers { get; set; } = [ "127.0.0.1:53" ];
    public TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromSeconds(120);
    public PositiveTtl PositiveTTl { get; set; } = PositiveTtl.Forever.Instance;
    
    /// <summary>
    /// Renders HOCON configuration based on current settings.
    /// </summary>
    /// <returns>HOCON configuration string.</returns>
    public static string FullPath(string path) => $"akka.io.dns.{path}";

    private string ToHocon()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath(ConfigPath)} {{");
        sb.AppendLine($"  provider-object = \"{Class.FullName}, {Class.Assembly.GetName().Name}\",");
        sb.Append($"  {NameserversPath} = [");
        var c = Nameservers.Count;  
        for (int i = 0; i < c; i++)
        {
            sb.Append($"\"{Nameservers[i]}\"");
            if (i < c - 1)
                sb.Append(", ");
        }
        sb.AppendLine("],");
        sb.AppendLine($"  cache-cleanup-interval = {CacheCleanupInterval.TotalSeconds}s,");
        sb.AppendLine($"  positive-ttl  = {PositiveTTl.ToString()},");
        
        sb.AppendLine("}");
            
        return sb.ToString();
    }
    /// <summary> </summary>
    /// <param name="builder"></param>
    /// <param name="setup"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void Apply(AkkaConfigurationBuilder builder, Setup? setup = null)
    {
        builder.AddHocon(ToHocon(), HoconAddMode.Prepend);
    }
    public string ConfigPath => DefaultPath;
    public Type Class => typeof(AsyncDnsProvider);
}
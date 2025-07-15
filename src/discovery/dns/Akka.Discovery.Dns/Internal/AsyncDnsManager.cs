using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Event;
using Akka.IO;
using Akka.Routing;
using Akka.Util;

namespace Akka.Discovery.Dns.Internal;

internal class AsyncDnsManager : ActorBase, IRequiresMessageQueue<IUnboundedMessageQueueSemantics>
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    // private readonly IActorRef _resolver;
    // private IPeriodicCacheCleanup _cacheCleanup;
    // private ICancelable _cleanupTimer;

    // private IReadOnlyList<string> _nameservers;
    private IActorRef[] _resolvers;
    private IActorRef _resolver;

    /// <summary>
    /// Creates a new instance of the AsyncDnsManager.
    /// </summary>
    /// <param name="ext">The DNS extension that owns this manager.</param>
    public AsyncDnsManager(DnsExt ext)
    {
        var nameservers = ext.Settings.ResolverConfig.GetStringList(AsyncDnsResolverOptions.NameserversPath)
            .ToImmutableList();
        if (nameservers.Count == 0)
        {
            throw NoNameServerConfigured.Instance;
        }
        _resolvers = SpawnClients(ext, nameservers);
        _resolver = Context.ActorOf(Props.Empty.WithRouter(new RoundRobinGroup(_resolvers.Select(x => x.Path.ToString()))), "dns-router");
    }
    IActorRef[] SpawnClients(DnsExt ext, IReadOnlyList<string> nameservers) => 
        nameservers
            .Select(ns =>
            {
                try
                {
                    return Option<(string name, EndPoint endpoint)>
                        .Create((ns,ParseEndPoint(ns)));
                }
                catch (Exception e)
                {
                    _log.Error(e, "Failed parsing nameserver from {0}", ns);
                    return Option<(string name, EndPoint endpoint)>.None;
                }
            })
            .Where(x => x.HasValue)
            .Select(opt =>
                Context.ActorOf(
                    Props.Create(typeof(DnsClient), opt.Value.endpoint)
                        .WithDeploy(Deploy.Local)
                        .WithDispatcher(ext.Settings.Dispatcher)
                    , opt.Value.name)
            ).ToArray();

    public class NoNameServerConfigured : Exception
    {
        private NoNameServerConfigured(string msg) : base (msg) {}
        public static readonly NoNameServerConfigured Instance = new ("Nameservers were not configured");
    }

    bool HandleRequest(object message)
    {
        _resolver.Forward(message);
        return true;
        // foreach (var resolver in _resolvers)
        // {
        //     resolver.Forward(message);
        // }
        //
        // return true;
    }

    /// <summary>
    /// Translate SimpleDnsManager resolve request into DnsClient.DnsQuestion
    /// </summary>
    /// <param name="r"></param>
    /// <returns></returns>
    DnsClient.DnsQuestion Convert(IO.Dns.Resolve r) => new(DnsClient.NewQueryId(), r.Name, DnsProtocol.RecordType.Any);
    
    /// <summary>
    /// Handles DNS resolution requests and cache cleanup messages.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <returns>True if the message was handled, false otherwise.</returns>
    protected override bool Receive(object message)
    {
        switch (message)
        {
            case IO.Dns.Resolve r:
            {
                return HandleRequest(Convert(r));
            }
            case DnsClient.DnsQuestion question:
                return HandleRequest(question);
            default:
                Unhandled(message);
                return false;
        }
    }

    /// <summary>
    /// Cancels the cleanup timer when the actor is stopped.
    /// </summary>
    protected override void PostStop()
    {
        // if (_cleanupTimer != null)
        //     _cleanupTimer.Cancel();
    }

    /// <summary>
    /// Message sent to trigger DNS cache cleanup.
    /// </summary>
    // internal class CacheCleanup
    // {
    //     /// <summary>
    //     /// Singleton instance of the cache cleanup message.
    //     /// </summary>
    //     public static readonly CacheCleanup Instance = new();
    // }

    /// <summary>
    /// Parse a string endpoint into an IPEndPoint
    /// Handles IPv4 addresses, IPv6 addresses, and hostnames with optional port
    /// </summary>
    /// <param name="endpoint">String in format "address:port" where address can be IPv4, IPv6, or hostname</param>
    /// <returns>IPEndPoint representing the parsed endpoint</returns>
    internal static EndPoint ParseEndPoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

        // Default port if not specified
        int port = 53;
        string host;

        // Check if we have an IPv6 address with brackets
        if (endpoint.StartsWith("["))
        {
            // Format is [IPv6]:port
            int closeBracketIndex = endpoint.IndexOf(']');
            if (closeBracketIndex == -1)
                throw new FormatException($"Invalid IPv6 endpoint format: {endpoint}. Expected [IPv6]:port");

            host = endpoint.Substring(1, closeBracketIndex - 1); // Extract IPv6 without brackets

            // Check if there's a port after the IPv6 address
            if (closeBracketIndex + 1 < endpoint.Length && endpoint[closeBracketIndex + 1] == ':')
            {
                string portStr = endpoint.Substring(closeBracketIndex + 2);
                if (!int.TryParse(portStr, out port))
                    throw new FormatException($"Invalid port in endpoint: {portStr}");
            }
        }
        else if (endpoint.Contains(":"))
        {
            // Could be IPv4:port or IPv6 without brackets
            if (endpoint.Count(c => c == ':') > 1)
            {
                // This is likely an IPv6 address without port
                host = endpoint;
            }
            else
            {
                // This is likely IPv4:port
                var parts = endpoint.Split(':');
                host = parts[0];
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    if (!int.TryParse(parts[1], out port))
                        throw new FormatException($"Invalid port in endpoint: {parts[1]}");
                }
            }
        }
        else
        {
            // Just a hostname or IP without port
            host = endpoint;
        }

        // Try to parse as IP address
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return new IPEndPoint(ipAddress, port);
        }

        // If not an IP, try to resolve hostname
        try
        {
            var addresses = System.Net.Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
                throw new FormatException($"Could not resolve hostname: {host}");

            // Prefer IPv4 address if available
            var preferredAddress =
                addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?? addresses[0]; // Fall back to first address if no IPv4

            return new IPEndPoint(preferredAddress, port);
        }
        catch (Exception ex) when (!(ex is FormatException))
        {
            throw new FormatException($"Failed to resolve hostname: {host}", ex);
        }
    }
}
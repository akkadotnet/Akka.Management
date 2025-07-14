using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery.Dns.Internal;
using Akka.Event;
using Akka.IO;

namespace Akka.Discovery.Dns;

/// <summary>
/// DNS-based service discovery implementation.
/// </summary>
public class DnsServiceDiscovery : ServiceDiscovery
{
    private readonly ILoggingAdapter _log;
    private readonly IActorRef _dns;
    private readonly ExtendedActorSystem _system;

    public DnsServiceDiscovery(ExtendedActorSystem system)
    {
        _system = system;
        _log = Logging.GetLogger(system, this);
        _dns = Akka.IO.Dns.Instance.CreateExtension(_system).Manager;
    }


    /// <summary>
    /// Cleans an IP string by removing leading '/' if present.
    /// </summary>
    private string CleanIpString(string ipString) =>
        ipString.StartsWith("/") ? ipString.Substring(1) : ipString;

    public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
    {
        if (!string.IsNullOrWhiteSpace(lookup.PortName) && !string.IsNullOrWhiteSpace(lookup.Protocol))
        {
            return await LookupSrv(lookup, resolveTimeout);
        }

        return await LookupIp(lookup, resolveTimeout);
    }

    private async Task<Resolved> LookupSrv(Lookup lookup, TimeSpan resolveTimeout)
    {
        var srvRequest = $"_{lookup.PortName}._{lookup.Protocol}.{lookup.ServiceName}";
        _log.Debug("Lookup [{0}] translated to SRV query [{1}] as contains portName and protocol", lookup, srvRequest);
        
        try
        {
            // Generate a random query ID
            short queryId = (short)new Random().Next(0, 65535);
            
            // Send SRV question and await response
            var result = await _dns.Ask<object>(new Internal.DnsClient.SrvQuestion(queryId, srvRequest), resolveTimeout);
            
            if (result is Internal.DnsClient.Answer answer)
            {
                return SrvRecordsToResolved(srvRequest, answer);
            }
            else if (result is Status.Failure failure)
            {
                throw failure.Cause;
            }
            
            _log.Warning("Unexpected response type from DNS resolver: {0}", result.GetType());
            return new Resolved(srvRequest, ImmutableList<ResolvedTarget>.Empty);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SRV lookup failed for {0}", srvRequest);
            throw;
        }
    }

    private async Task<Resolved> LookupIp(Lookup lookup, TimeSpan resolveTimeout)
    {
        _log.Debug("Lookup[{0}] translated to A/AAAA lookup as does not have portName and protocol", lookup);
        
        // For standard IP lookups, continue to use the built-in Akka.IO.Dns resolver
        return await AskResolveIp(lookup.ServiceName, resolveTimeout);
    }

    private async Task<Resolved> AskResolveIp(string serviceName, TimeSpan timeout)
    {
        try
        {
            var result = await _dns.Ask<object>(new Akka.IO.Dns.Resolve(serviceName), timeout);

            if (result is IO.Dns.Resolved resolved)
            {
                if (resolved.IsSuccess)
                {
                    _log.Debug("lookup result: {0}", resolved);
                    return IpRecordsToResolved(serviceName, resolved);
                }
                
                _log.Error(resolved.Exception, "Failed to resolve serviceName: {0}", serviceName);
                return new Resolved(serviceName, ImmutableList<ResolvedTarget>.Empty);
            }

            _log.Warning("Resolved UNEXPECTED (resolving to Nil): {0}", result.GetType());
            return new Resolved(serviceName, ImmutableList<ResolvedTarget>.Empty);
        }
        catch (AskTimeoutException)
        {
            throw new TimeoutException($"Dns resolve did not respond within {timeout}");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during DNS resolution");
            throw;
        }
    }

    // private async Task<Resolved> AskResolve(string srvRequest, TimeSpan timeout)
    // {
    //     try
    //     {
    //         var result = await _dns.Ask<object>(new IO.Dns.Resolve(srvRequest), timeout);
    //
    //         if (result is IO.Dns.Resolved resolved)
    //         {
    //             _log.Debug("Lookup result: {0}", resolved);
    //             return SrvRecordsToResolved(srvRequest, resolved);
    //         }
    //
    //         _log.Warning("Resolved UNEXPECTED (resolving to Nil): {0}", result.GetType());
    //         return new Resolved(srvRequest, ImmutableList<ResolvedTarget>.Empty);
    //     }
    //     catch (AskTimeoutException)
    //     {
    //         throw new TimeoutException($"Dns resolve did not respond within {timeout}");
    //     }
    //     catch (Exception ex)
    //     {
    //         _log.Error(ex, "Error during DNS resolution");
    //         throw;
    //     }
    // }

    /// <summary>
    /// Converts SRV records to a Resolved object from our custom DNS client response.
    /// </summary>
    private Resolved SrvRecordsToResolved(string srvRequest, Internal.DnsClient.Answer resolved)
    {
        var ips = new Dictionary<string, IList<IPAddress>>();
        
        // Process SRV records
        var srvRecords = resolved.Records.OfType<Internal.SrvRecord>().ToList();
        
        // Process additional A/AAAA records for hostname resolution
        foreach (var aRecord in resolved.AdditionalRecords.OfType<Internal.ARecord>())
        {
            if (!ips.TryGetValue(aRecord.Name, out var aIps))
            {
                aIps = new List<IPAddress>();
                ips[aRecord.Name] = aIps;
            }
            
            aIps.Add(aRecord.Ip);
        }
        
        foreach (var aaaaRecord in resolved.AdditionalRecords.OfType<Internal.AaaaRecord>())
        {
            if (!ips.TryGetValue(aaaaRecord.Name, out var aaaaIps))
            {
                aaaaIps = new List<IPAddress>();
                ips[aaaaRecord.Name] = aaaaIps;
            }
            
            aaaaIps.Add(aaaaRecord.Ip);
        }

        // Build the list of resolved targets from SRV records
        var targets = new List<ResolvedTarget>();
        
        foreach (var record in srvRecords)
        {
            // Remove trailing dot if present
            string targetHost = record.Target.EndsWith(".") 
                ? record.Target.Substring(0, record.Target.Length - 1) 
                : record.Target;
                
            // Try to get IP from additional records
            if (ips.TryGetValue(targetHost, out var hostIps) || ips.TryGetValue(targetHost + ".", out hostIps))
            {
                foreach (var ip in hostIps)
                {
                    targets.Add(new ResolvedTarget(targetHost, record.Port, ip));
                }
            }
            else
            {
                // If we don't have the IP, just use the hostname
                targets.Add(new ResolvedTarget(targetHost, record.Port));
            }
        }
        return new Resolved(srvRequest, targets.ToImmutableList());
    }

    /// <summary>
    /// Converts IP records to a Resolved object.
    /// </summary>
    private Resolved IpRecordsToResolved(string serviceName, Akka.IO.Dns.Resolved resolved)
    {
        var addresses =
            new[]
            {
                resolved.Ipv4.Select(aRecord => 
                    new ResolvedTarget(CleanIpString(aRecord.ToString()), null, aRecord)),
                resolved.Ipv6.Select(aaaaRecord =>
                    new ResolvedTarget(CleanIpString(aaaaRecord.ToString()), null, aaaaRecord))
            }
                .SelectMany(x => x)
                .ToImmutableList();

        return new Resolved(serviceName, addresses);
    }
}

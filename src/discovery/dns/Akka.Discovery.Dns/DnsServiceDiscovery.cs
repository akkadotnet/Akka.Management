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
    private static string CleanIpString(string ipString) =>
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
            // Send SRV question and await response
            var result = await _dns.Ask<object>(new Internal.AsyncDnsClient.DnsQuestion(AsyncDnsClient.NewQueryId(), srvRequest, DnsProtocol.RecordType.Srv), resolveTimeout);
            
            if (result is DnsProtocol.Message answer)
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
    
    private async Task<Resolved> LookupIp(Lookup lookup, TimeSpan timeout)
    {
        try
        {
            _log.Debug("Lookup[{0}] translated to A/AAAA lookup as does not have portName and protocol", lookup.ServiceName);
            
            // use IO.Dns.Resolve for compatibility with both InetAddressResolver and AsyncDnsClient
            var result = await _dns.Ask<object>(new Akka.IO.Dns.Resolve(lookup.ServiceName), timeout);

            //inet-address response
            if (result is IO.Dns.Resolved resolved)
            {
                if (resolved.IsSuccess)
                {
                    var parsed = IoDnsMessageToResolved(lookup.ServiceName, resolved);
                    _log.Debug("lookup result: {0}", parsed);
                    return parsed;
                }
                
                _log.Error(resolved.Exception, "Failed to resolve serviceName: {0}", lookup.ServiceName);
                return new Resolved(lookup.ServiceName, ImmutableList<ResolvedTarget>.Empty);
            }
            //async-dns response
            if (result is DnsProtocol.Message answer)
            {
                if (answer.Flags.ResponseCode == DnsProtocol.ResponseCode.Success)
                {
                    var parsed = DnsMessageToResolved(lookup.ServiceName, answer);
                    _log.Debug("lookup result: {0}", parsed);
                    return parsed;
                }
                _log.Error("Failed to resolve serviceName=[{0}], answer=[{1}]", lookup.ServiceName, answer);
                return new Resolved(lookup.ServiceName, ImmutableList<ResolvedTarget>.Empty);
            }

            _log.Warning("Resolved UNEXPECTED (resolving to Nil): {0}", result.GetType());
            return new Resolved(lookup.ServiceName, ImmutableList<ResolvedTarget>.Empty);
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

    /// <summary>
    /// Converts SRV records to a Resolved object from our custom DNS client response.
    /// </summary>
    private static Resolved SrvRecordsToResolved(string srvRequest, Internal.DnsProtocol.Message resolved)
    {
        var ips = new Dictionary<string, IList<IPAddress>>();
        
        // Process SRV records
        var srvRecords = resolved.AnswerRecords.OfType<Internal.SrvRecord>().ToList();
        
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
    
    private static  Resolved IpsToResolved(string serviceName, IEnumerable<IPAddress>resolved) =>
        new(
            serviceName, 
            resolved.Select(ipAddress =>
                    new ResolvedTarget(CleanIpString(ipAddress.ToString()), null, ipAddress))
                .ToImmutableList()
        );
    
    private static Resolved IoDnsMessageToResolved(string serviceName, Akka.IO.Dns.Resolved resolved) =>
        IpsToResolved(serviceName, 
            new[]
            {
                resolved.Ipv4, 
                resolved.Ipv6
            }.SelectMany(x => x));
    
    private static Resolved DnsMessageToResolved(string serviceName, DnsProtocol.Message resolved) =>
        IpsToResolved(serviceName,
            new[]
                {
                    DnsProtocol.Message.ToIpAddresses(resolved, DnsProtocol.RecordType.A),
                    DnsProtocol.Message.ToIpAddresses(resolved, DnsProtocol.RecordType.Aaaa)
                }
                .SelectMany(x => x));
}

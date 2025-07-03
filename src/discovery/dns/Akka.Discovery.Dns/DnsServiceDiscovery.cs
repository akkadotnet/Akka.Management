using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.IO;

namespace Akka.Discovery.Dns;

/// <summary>
/// DNS-based service discovery implementation.
/// </summary>
public class DnsServiceDiscovery : ServiceDiscovery
{
    private readonly ILoggingAdapter _log;
    private readonly DnsExt _dns;
    private readonly ExtendedActorSystem _system;

    public DnsServiceDiscovery(ExtendedActorSystem system)
    {
        _system = system;
        _log = Logging.GetLogger(system, typeof(DnsServiceDiscovery));

        var dnsResolver = _system.Settings.Config.GetString("akka.io.dns.resolver");
        switch (dnsResolver)
        {
            case "inet-address": 
                _dns = Akka.IO.Dns.Instance.CreateExtension(_system);
                break;
            default:
                throw new NotImplementedException();

        }
    }


    /// <summary>
    /// Cleans an IP string by removing leading '/' if present.
    /// </summary>
    private string CleanIpString(string ipString) =>
        ipString.StartsWith("/") ? ipString.Substring(1) : ipString;

    public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
    {
        if (!string.IsNullOrWhiteSpace(lookup.PortName) && !string.IsNullOrWhiteSpace(lookup.Protocol))
            return await LookupSrv(lookup, resolveTimeout);
        else
            return await LookupIp(lookup, resolveTimeout);
    }

    private async Task<Resolved> LookupSrv(Lookup lookup, TimeSpan resolveTimeout)
    {
        var srvRequest = $"_{lookup.PortName}._{lookup.Protocol}.{lookup.ServiceName}";
        _log.Debug("Lookup [{0}] translated to SRV query [{1}] as contains portName and protocol", lookup, srvRequest);
        var resolved = _dns.Cache.Cached(srvRequest);
        if (resolved == null)
        {
            return await AskResolve(srvRequest, resolveTimeout);
        }
        return SrvRecordsToResolved(srvRequest, resolved);
    }

    private async Task<Resolved> LookupIp(Lookup lookup, TimeSpan resolveTimeout)
    {
        _log.Debug("Lookup[{0}] translated to A/AAAA lookup as does not have portName and protocol", lookup);
        
        var resolved = _dns.Cache.Cached(lookup.ServiceName);
        if (resolved == null)
        {
            return await AskResolveIp(lookup.ServiceName, resolveTimeout);
        }
        return IpRecordsToResolved(lookup.ServiceName, resolved);
    }

    private async Task<Resolved> AskResolveIp(string serviceName, TimeSpan timeout)
    {
        try
        {
            var result = await _dns.Manager.Ask<object>(new Akka.IO.Dns.Resolve(serviceName), timeout);

            if (result is IO.Dns.Resolved resolved)
            {
                _log.Debug("lookup result: {0}", resolved);
                return IpRecordsToResolved(serviceName, resolved);
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

    private async Task<Resolved> AskResolve(string srvRequest, TimeSpan timeout)
    {
        try
        {
            var result = await _dns.Manager.Ask<object>(new IO.Dns.Resolve(srvRequest), timeout);

            if (result is IO.Dns.Resolved resolved)
            {
                _log.Debug("Lookup result: {0}", resolved);
                return SrvRecordsToResolved(srvRequest, resolved);
            }

            _log.Warning("Resolved UNEXPECTED (resolving to Nil): {0}", result.GetType());
            return new Resolved(srvRequest, ImmutableList<ResolvedTarget>.Empty);
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
    /// Converts SRV records to a Resolved object.
    /// </summary>
    private Resolved SrvRecordsToResolved(string srvRequest, Akka.IO.Dns.Resolved resolved)
    {
        // var ips = new Dictionary<string, IList<IPAddress>>();

        // Build a map of hostname to IP addresses from additional records
        // foreach (var aRecord in resolved.Ipv4)
        // {
        //     if (!ips.TryGetValue(aRecord.Name, out var aIps))
        //     {
        //         aIps = new List<IPAddress>();
        //         ips[aRecord.Name] = aIps;
        //     }
        //
        //     aIps.Add(aRecord.Ip);
        // }
        // foreach (var record in resolved.Ipv6) {
        //             if (!ips.TryGetValue(aaaaRecord.Name, out var aaaaIps))
        //             {
        //                 aaaaIps = new List<IPAddress>();
        //                 ips[aaaaRecord.Name] = aaaaIps;
        //             }
        //
        //             aaaaIps.Add(aaaaRecord.Ip);
        //             break;
        // }
        //
        // var addresses = resolved.Records.OfType<SrvRecord>()
        //     .SelectMany(srv =>
        //     {
        //         if (ips.TryGetValue(srv.Target, out var ipList) && ipList.Count > 0)
        //         {
        //             return ipList.Select(ip => new ResolvedTarget(srv.Target, srv.Port, ip));
        //         }
        //         else
        //         {
        //             return new[] { new ResolvedTarget(srv.Target, srv.Port, null) };
        //         }
        //     })
        //     .ToImmutableList();

        return new Resolved(srvRequest, []);
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

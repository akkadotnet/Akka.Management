using System;
using System.IO;
using System.Net;
using System.Text;

namespace Akka.Discovery.Dns.Internal;

/// <summary>
/// Base class for DNS resource records
/// </summary>

public abstract record ResourceRecord(
    string Name,
    DnsProtocol.RecordType Type,
    DnsProtocol.RecordClass Class,
    uint TimeToLive)
{
    public abstract byte[] WriteData();
    
    
    internal static void WriteDomainName(BinaryWriter writer, string name)
    {
        if (string.IsNullOrEmpty(name) || name == ".")
        {
            writer.Write((byte)0);
            return;
        }

        string normalizedName = name.EndsWith(".") ? name : name + ".";
        string[] labels = normalizedName.Split('.');

        foreach (string label in labels)
        {
            if (!string.IsNullOrEmpty(label))
            {
                byte[] labelBytes = Encoding.ASCII.GetBytes(label);
                writer.Write((byte)labelBytes.Length);
                writer.Write(labelBytes);
            }
        }

        writer.Write((byte)0);
    }
}

/// <summary>
/// IPv4 address record (A)
/// </summary>
public sealed record ARecord(
    string Name,
    DnsProtocol.RecordClass Class,
    uint TimeToLive,
    IPAddress Ip)
    : ResourceRecord(Name, DnsProtocol.RecordType.A, Class, TimeToLive)
{
    public override byte[] WriteData()
    {
        return Ip.GetAddressBytes();
    }
}

/// <summary>
/// IPv6 address record (AAAA)
/// </summary>
public sealed record AaaaRecord(
    string Name,
    DnsProtocol.RecordClass Class,
    uint TimeToLive,
    IPAddress Ip)
    : ResourceRecord(Name, DnsProtocol.RecordType.Aaaa, Class, TimeToLive)
{
    public override byte[] WriteData()
    {
        return Ip.GetAddressBytes();
    }
}

/// <summary>
/// Canonical name record (CNAME)
/// </summary>
public sealed record CnameRecord (
    string Name,
    DnsProtocol.RecordClass Class,
    uint TimeToLive,
    string CanonicalName) 
    : ResourceRecord(Name, DnsProtocol.RecordType.Cname, Class, TimeToLive)
{
     public override byte[] WriteData()
     {
         using (var ms = new MemoryStream())
         using (var writer = new BinaryWriter(ms))
         {
             WriteDomainName(writer, CanonicalName);
             return ms.ToArray();
         }
     }
}

/// <summary>
/// Service record (SRV)
/// </summary>
public sealed record SrvRecord(
    string Name,
    DnsProtocol.RecordClass Class,
    uint TimeToLive,
    ushort Priority,
    ushort Weight,
    ushort Port,
    string Target)
    : ResourceRecord(Name, DnsProtocol.RecordType.Srv, Class, TimeToLive)
{
     public override byte[] WriteData()
     {
         using var ms = new MemoryStream();
         using var writer = new BinaryWriter(ms);
         writer.Write(IPAddress.HostToNetworkOrder((short)Priority));
         writer.Write(IPAddress.HostToNetworkOrder((short)Weight));
         writer.Write(IPAddress.HostToNetworkOrder((short)Port));
         WriteDomainName(writer, Target);
         return ms.ToArray();
     }
}

/// <summary>
/// Generic record for types not specifically implemented
/// </summary>
public sealed record UnknownRecord(
    string Name,
    DnsProtocol.RecordType Type,
    DnsProtocol.RecordClass Class,
    uint TimeToLive,
    byte[] Data)
    : ResourceRecord(Name, Type, Class, TimeToLive)
{
    public override byte[] WriteData() => Data;

}
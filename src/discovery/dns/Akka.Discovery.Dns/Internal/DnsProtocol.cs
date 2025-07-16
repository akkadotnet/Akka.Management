using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Akka.Util;

namespace Akka.Discovery.Dns.Internal;

/// <summary>
/// DNS protocol implementation supporting SRV records
/// </summary>
public static class DnsProtocol
{
    /// <summary>
    /// DNS record types
    /// </summary>
    public enum RecordType : ushort
    {
        A = 1,       // IPv4 address record
        Ns = 2,      // Nameserver record
        Cname = 5,   // Canonical name record
        Soa = 6,     // Start of authority record
        Ptr = 12,    // Pointer record
        Mx = 15,     // Mail exchange record
        Txt = 16,    // Text record
        Aaaa = 28,   // IPv6 address record
        Srv = 33,    // Service record
        Any = 255    // Any record type
    }

    /// <summary>
    /// DNS record classes
    /// </summary>
    public enum RecordClass : ushort
    {
        In = 1,      // Internet
        Cs = 2,      // CSNET
        Ch = 3,      // CHAOS
        Hs = 4,      // Hesiod
        Any = 255    // Any class
    }

    /// <summary>
    /// DNS response codes
    /// </summary>
    public enum ResponseCode : byte
    {
        Success = 0,
        FormatError = 1,
        ServerFailure = 2,
        NameError = 3,
        NotImplemented = 4,
        Refused = 5
    }

    /// <summary>
    /// DNS message flags
    /// </summary>
    public record MessageFlags
    {
        public bool IsResponse { get; init; }
        public byte OpCode { get; init; }
        public bool IsAuthoritativeAnswer { get; init; }
        public bool IsTruncated { get; init; }
        public bool IsRecursionDesired { get; init; }
        public bool IsRecursionAvailable { get; init; }
        public ResponseCode ResponseCode { get; init; }

        public MessageFlags()
        {
            // Default values for a query
            IsResponse = false;
            OpCode = 0;
            IsAuthoritativeAnswer = false;
            IsTruncated = false;
            IsRecursionDesired = true;
            IsRecursionAvailable = false;
            ResponseCode = ResponseCode.Success;
        }

        /// <summary>
        /// Parse message flags from a 16-bit value
        /// </summary>
        public static MessageFlags FromUInt16(ushort flags)
        {
            return new MessageFlags
            {
                IsResponse = (flags & 0x8000) != 0,
                OpCode = (byte)((flags >> 11) & 0xF),
                IsAuthoritativeAnswer = (flags & 0x0400) != 0,
                IsTruncated = (flags & 0x0200) != 0,
                IsRecursionDesired = (flags & 0x0100) != 0,
                IsRecursionAvailable = (flags & 0x0080) != 0,
                ResponseCode = (ResponseCode)(flags & 0xF)
            };
        }

        /// <summary>
        /// Convert message flags to a 16-bit value
        /// </summary>
        public ushort ToUInt16()
        {
            ushort flags = 0;

            if (IsResponse) flags |= 0x8000;
            flags |= (ushort)((OpCode & 0xF) << 11);
            if (IsAuthoritativeAnswer) flags |= 0x0400;
            if (IsTruncated) flags |= 0x0200;
            if (IsRecursionDesired) flags |= 0x0100;
            if (IsRecursionAvailable) flags |= 0x0080;
            flags |= (ushort)((int)ResponseCode & 0xF);

            return flags;
        }

        public override string ToString()
        {
            return $"MessageFlags(response={IsResponse}, opCode={OpCode}, aa={IsAuthoritativeAnswer}, " +
                   $"tc={IsTruncated}, rd={IsRecursionDesired}, ra={IsRecursionAvailable}, rcode={ResponseCode})";
        }
    }

    /// <summary>
    /// DNS question
    /// </summary>
    public class Question
    {
        public string Name { get; }
        public RecordType Type { get; }
        public RecordClass Class { get; }

        public Question(string name, RecordType type, RecordClass @class)
        {
            Name = name;
            Type = type;
            Class = @class;
        }

        public override string ToString()
        {
            return $"Question({Name}, {Type}, {Class})";
        }
    }

    /// <summary>
    /// DNS message
    /// </summary>
    public record Message
    {
        public short Id { get; }
        public MessageFlags Flags { get; }
        public ImmutableList<Question> Questions { get; }
        public ImmutableList<Akka.Discovery.Dns.Internal.ResourceRecord> AnswerRecords { get; }
        public ImmutableList<Akka.Discovery.Dns.Internal.ResourceRecord> AuthorityRecords { get; }
        public ImmutableList<Akka.Discovery.Dns.Internal.ResourceRecord> AdditionalRecords { get; }

        public Message(
            short id,
            MessageFlags flags,
            ImmutableList<Question> questions,
            ImmutableList<ResourceRecord>? answerRecords = null,
            ImmutableList<ResourceRecord>? authorityRecords = null,
            ImmutableList<ResourceRecord>? additionalRecords = null)
        {
            Id = id;
            Flags = flags ?? new MessageFlags();
            Questions = questions;
            AnswerRecords = answerRecords ?? ImmutableList<ResourceRecord>.Empty;
            AuthorityRecords = authorityRecords ?? ImmutableList<ResourceRecord>.Empty;
            AdditionalRecords = additionalRecords ?? ImmutableList<ResourceRecord>.Empty;
        }

        public override string ToString()
        {
            return $"Message(id={Id}, flags={Flags}, questions=[{string.Join(", ", Questions)}], " +
                   $"answers=[{string.Join(", ", AnswerRecords)}], " +
                   $"authority=[{string.Join(", ", AuthorityRecords)}], " +
                   $"additional=[{string.Join(", ", AdditionalRecords)}])";
        }

        /// <summary>
        /// Write the DNS message to a byte array
        /// </summary>
        public byte[] Write()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write header
                writer.Write(IPAddress.HostToNetworkOrder((short)Id));
                writer.Write(IPAddress.HostToNetworkOrder((short)Flags.ToUInt16()));
                writer.Write(IPAddress.HostToNetworkOrder((short)Questions.Count));
                writer.Write(IPAddress.HostToNetworkOrder((short)AnswerRecords.Count));
                writer.Write(IPAddress.HostToNetworkOrder((short)AuthorityRecords.Count));
                writer.Write(IPAddress.HostToNetworkOrder((short)AdditionalRecords.Count));

                // Write questions
                foreach (var question in Questions)
                {
                    WriteDomainName(writer, question.Name);
                    writer.Write(IPAddress.HostToNetworkOrder((short)question.Type));
                    writer.Write(IPAddress.HostToNetworkOrder((short)question.Class));
                }

                // Write resource records
                WriteResourceRecords(writer, AnswerRecords);
                WriteResourceRecords(writer, AuthorityRecords);
                WriteResourceRecords(writer, AdditionalRecords);

                return ms.ToArray();
            }
        }

        public string FirstQuestionName
        {
            get
            {
                if (Questions.IsEmpty)
                    return string.Empty;
                return Questions[0].Name;
            }
        }

        /// <summary>
        /// Parse a DNS message from a byte array
        /// </summary>
        public static Message Parse(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Read header
                short id = IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort flags = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort questionCount = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort answerCount = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort authorityCount = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort additionalCount = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());

                var messageFlags = MessageFlags.FromUInt16(flags);

                // Read questions
                var questions = ImmutableList.CreateBuilder<Question>();
                for (int i = 0; i < questionCount; i++)
                {
                    string name = ReadDomainName(reader, ms);
                    ushort type = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                    ushort @class = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());

                    questions.Add(new Question(name, (RecordType)type, (RecordClass)@class));
                }

                // Read resource records
                var answerRecords = ReadResourceRecords(reader, ms, answerCount);
                var authorityRecords = ReadResourceRecords(reader, ms, authorityCount);
                var additionalRecords = ReadResourceRecords(reader, ms, additionalCount);

                return new Message(
                    id,
                    messageFlags,
                    questions.ToImmutable(),
                    answerRecords,
                    authorityRecords,
                    additionalRecords);
            }
        }

        /// <summary>
        /// Write domain name in DNS format
        /// </summary>
        private static void WriteDomainName(BinaryWriter writer, string name)
        {
            if (string.IsNullOrEmpty(name) || name == ".")
            {
                // Root domain
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

            // Terminate with root label
            writer.Write((byte)0);
        }

        /// <summary>
        /// Read domain name in DNS format, handling compression
        /// </summary>
        private static string ReadDomainName(BinaryReader reader, MemoryStream ms)
        {
            List<string> labels = new List<string>();
            int length;

            while ((length = reader.ReadByte()) > 0)
            {
                // Check for compression pointer
                if ((length & 0xC0) == 0xC0)
                {
                    int pointer = ((length & 0x3F) << 8) | reader.ReadByte();
                    long currentPosition = ms.Position;
                    ms.Position = pointer;
                        
                    // Recursively read the pointed-to name
                    string pointerName = ReadDomainName(reader, ms);
                        
                    // Restore position and return combined name
                    ms.Position = currentPosition;
                        
                    if (labels.Count > 0)
                    {
                        return string.Join(".", labels) + "." + pointerName;
                    }
                    return pointerName;
                }

                // Regular label
                byte[] labelBytes = reader.ReadBytes(length);
                labels.Add(Encoding.ASCII.GetString(labelBytes));
            }

            return string.Join(".", labels);
        }

        /// <summary>
        /// Write resource records
        /// </summary>
        private static void WriteResourceRecords(BinaryWriter writer, IEnumerable<ResourceRecord> records)
        {
            foreach (var record in records)
            {
                WriteDomainName(writer, record.Name);
                writer.Write(IPAddress.HostToNetworkOrder((short)record.Type));
                writer.Write(IPAddress.HostToNetworkOrder((short)record.Class));
                writer.Write(IPAddress.HostToNetworkOrder((int)record.TimeToLive));

                // Write record data
                byte[] data = record.WriteData();
                writer.Write(IPAddress.HostToNetworkOrder((short)data.Length));
                writer.Write(data);
            }
        }

        /// <summary>
        /// Read resource records
        /// </summary>
        private static ImmutableList<ResourceRecord> ReadResourceRecords(
            BinaryReader reader, MemoryStream ms, int count)
        {
            var records = ImmutableList.CreateBuilder<ResourceRecord>();

            for (int i = 0; i < count; i++)
            {
                string name = ReadDomainName(reader, ms);
                ushort type = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort @class = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                uint ttl = (uint)IPAddress.NetworkToHostOrder(reader.ReadInt32());
                ushort dataLength = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());

                // Read the raw data
                byte[] data = reader.ReadBytes(dataLength);

                // Create appropriate resource record based on type
                ResourceRecord record;
                switch ((RecordType)type)
                {
                    case RecordType.A:
                        record = new ARecord(name, (RecordClass)@class, ttl, new IPAddress(data));
                        break;
                    case RecordType.Aaaa:
                        record = new AaaaRecord(name, (RecordClass)@class, ttl, new IPAddress(data));
                        break;
                    case RecordType.Cname:
                        record = new CnameRecord(name, (RecordClass)@class, ttl,
                            ReadDomainNameFromData(data));
                        break;
                    case RecordType.Srv:
                        record = ReadSrvRecord(name, (RecordClass)@class, ttl, data);
                        break;
                    default:
                        record = new UnknownRecord(name, (RecordType)type, (RecordClass)@class, ttl, data);
                        break;
                }

                records.Add(record);
            }

            return records.ToImmutable();
        }

        /// <summary>
        /// Read domain name from raw record data
        /// </summary>
        private static string ReadDomainNameFromData(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return ReadDomainName(reader, ms);
            }
        }

        /// <summary>
        /// Read SRV record from raw data
        /// </summary>
        private static SrvRecord ReadSrvRecord(string name, RecordClass @class, uint ttl, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                ushort priority = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort weight = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                ushort port = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                string target = ReadDomainName(reader, ms);

                return new SrvRecord(name, @class, ttl, priority, weight, port, target);
            }
        }
        
        /// <summary>
        /// Find minimal TTL value
        /// </summary>
        /// <param name="answer"></param>
        /// <returns></returns>
        public static uint MinTtl(Message answer)
        {
            uint rm = UInt32.MaxValue;
            uint arm = UInt32.MaxValue;
            if(answer.AnswerRecords.IsEmpty == false) 
                rm = answer.AnswerRecords.Select(x => x.TimeToLive).Min();
            if(answer.AdditionalRecords.IsEmpty == false) 
                arm = answer.AdditionalRecords.Select(x => x.TimeToLive).Min();
            if (rm == UInt32.MaxValue && arm == UInt32.MaxValue)
                return 0;
            return rm < arm ? rm : arm;
        }
        
        public static IEnumerable<ResourceRecord> RecordsOfType(Message answer, DnsProtocol.RecordType recordType) =>
            new[]
            {
                answer.AnswerRecords.Where(x => x.Type == recordType),
                answer.AdditionalRecords.Where(x => x.Type == recordType)
            }
                .SelectMany(x => x);
        
        public static IEnumerable<IPAddress> ToIpAddresses(Message answer, DnsProtocol.RecordType recordType)  =>
            RecordsOfType(answer, recordType)
                .Select(x => 
                    x switch { 
                        AaaaRecord aaaa => aaaa.Ip, 
                        ARecord a => a.Ip,
                        
                        _ => 
                        IPAddress.TryParse(x.Name, out var ip) 
                        ? Option<IPAddress>.Create(ip) 
                        : Option<IPAddress>.None }) //this might lose data if answer is hostname 
                .Where(x => x.HasValue)
                .Select(x => x.Value);
        
        /// <summary>
        /// Combine two DNS messages (typically one with A records and one with AAAA records)
        /// </summary>
        public static Message CombineResponses(Message message1, Message message2) =>
            new(
                message1.Id,
                message1.Flags,
                message1.Questions.AddRange(message2.Questions),
                message1.AnswerRecords.AddRange(message2.AnswerRecords),
                message1.AuthorityRecords.AddRange(message2.AuthorityRecords),
                message1.AdditionalRecords.AddRange(message2.AdditionalRecords));
    }
}
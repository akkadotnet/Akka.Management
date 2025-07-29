using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Akka.Actor;
using Akka.Event;
using Akka.IO;
using Akka.Pattern;
using Akka.Routing;

namespace Akka.Discovery.Dns.Internal;

/// <summary>
/// DNS client actor for resolving DNS queries, including SRV records.
/// This is an internal implementation for the Akka.Discovery.Dns service.
/// </summary>
internal class AsyncDnsClient(AsyncDnsCache cache, Configuration.Config config, EndPoint nameserver)
    : UntypedActorWithStash
{
    #region Messages and internal types 
    /// <summary>
    /// Base class for DNS questions
    /// </summary>
    public record DnsQuestion(string Name, DnsProtocol.RecordType RecordType) : IConsistentHashable
    {
        public object ConsistentHashKey { get; } = Name; //not sure is this it the right approach 
    }

    /// <summary>
    /// Message indicating TCP connection dropped
    /// </summary>
    public sealed record TcpDropped
    {
        private TcpDropped()  { }
        public static readonly TcpDropped Instance = new();
    }

    /// <summary>
    /// Information about an in-flight DNS request
    /// </summary>
    private class InFlightRequest(
        IActorRef replyTo,
        DnsProtocol.Message message,
        bool tcpRequest = false,
        short? linkedRequestId = null)
    {
        public IActorRef ReplyTo { get; } = replyTo;
        public DnsProtocol.Message Message { get; } = message;
        public DnsProtocol.Message? Response { get; set; }
        public bool TcpRequest { get; set; } = tcpRequest;
        public short? LinkedRequestId { get; set; } = linkedRequestId;
    }
    

    #endregion

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _tcpManager = Akka.IO.Tcp.Manager(Context.System);
    private readonly IActorRef _udpManager = Akka.IO.Udp.Instance.Apply(Context.System).Manager;
    private readonly IStash _stash = Context.CreateStash(typeof(AsyncDnsClient));
        
    // Tracks in-flight DNS requests
    private Dictionary<short, InFlightRequest> _inflightRequests = new();
    private IActorRef? _tcpDnsClient;
    private IActorRef? _udpSocket;
    private readonly PositiveTtl _positiveTtl = PositiveTtl.ParseFromConfig(config);
    private static readonly Random Random = new();
    
    protected override void PreStart()
    {
            // Bind to UDP port for DNS resolution
            _udpManager.Tell(new Udp.Bind(Self, new IPEndPoint(IPAddress.Any, 0)));

            // Create TCP client for fallback when UDP responses are truncated
            _tcpDnsClient = CreateTcpClient();
    }

    protected override void Unhandled(object message)
    {
        _log.Error( "Unhandled message: [{0}]",message);
        base.Unhandled(message);
    }

    protected override void OnReceive(object message)
    {
        // _log.Debug("Received message:[{0}]", message);
        switch (message)
        {
            case Udp.Bound bound:
                _log.Debug("Bound to UDP address [{0}]", bound.LocalAddress);
                _udpSocket = Context.Sender;
                Context.Become(Ready);
                _stash.UnstashAll();
                break;
            case DnsQuestion _:
                _stash.Stash();
                break;
            case IO.Dns.Resolve r:
                _stash.Stash();
                break;
            default:
                Unhandled(message);
                break;
        }
    }

    protected virtual void Ready(object message)
    {
        switch (message)
        {
            case DnsQuestion question:
                HandleQuestion(question.Name, question.RecordType);
                break;
            case Udp.Received received:
                try
                {
                    var msg = DnsProtocol.Message.Parse(received.Data.ToArray());
                    _log.Debug("Decoded UDP DNS response [{0}]", msg);

                    if (msg.Flags.IsTruncated)
                    {
                        _log.Debug("DNS response truncated, falling back to TCP");
                        if (_inflightRequests.TryGetValue(msg.Id, out var inFlight))
                        {
                            inFlight.TcpRequest = true;
                            _tcpDnsClient.Tell(inFlight.Message);
                        }
                        else
                        {
                            _log.Debug("Client for id {0} not found. Discarding unsuccessful response.", msg.Id);
                        }
                    }
                    else
                    {
                        Self.Tell(msg);   
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error processing DNS response");
                }
                break;


            case DnsProtocol.Message msg:
            {
                if (msg.Flags.ResponseCode != DnsProtocol.ResponseCode.Success)
                {
                    _log.Warning("DNS response failed: [{0}]", msg);
                }

                if (_inflightRequests.TryGetValue(msg.Id, out var request))
                {
                    var sentQuestions = request.Message.Questions.SelectMany(WithAndWithoutTrailingDots).ToArray()
                        .ToImmutableArray();
                    var answeredQuestions = msg.Questions.SelectMany(WithAndWithoutTrailingDots).ToImmutableArray();

                    if (answeredQuestions.Length == 0 || sentQuestions.Intersect(answeredQuestions).Any())
                    {
                        // Check if this is part of a linked request that needs both A and AAAA records
                        if (request.LinkedRequestId.HasValue)
                        {
                            var linkedId = request.LinkedRequestId.Value;
                            if (_inflightRequests.TryGetValue(linkedId, out var linkedReq))
                            {

                                if (linkedReq.Response != null)
                                {
                                    // We have both responses now, combine them
                                    var combinedMessage =
                                        DnsProtocol.Message.CombineResponses(linkedReq.Response, msg);
                                    request.ReplyTo.Tell(combinedMessage);

                                    // Clean up
                                    _inflightRequests.Remove(msg.Id);
                                    _inflightRequests.Remove(linkedId);

                                    // Cache the combined results
                                    if (GetCacheTtl(combinedMessage, out long combinedTtl))
                                        cache.Put(combinedMessage, combinedTtl);
                                }
                                else
                                {
                                    request.Response = msg;
                                    // Cache first result
                                    if (GetCacheTtl(msg, out long combinedTtl))
                                        cache.Put(msg, combinedTtl);
                                }

                            }
                            // We're waiting for the other response
                        }
                        else
                        {
                            // This is a regular request, not part of a resolve context
                            request.ReplyTo.Tell(msg);
                            _inflightRequests.Remove(msg.Id);

                            if (GetCacheTtl(msg, out long ttl))
                                cache.Put(msg, ttl);
                        }
                    }
                    else
                    {
                        _log.Warning(
                            "Martian DNS response for id [{0}]. Expected names [{1}], received names [{2}]. Discarding response",
                            msg.Id,
                            string.Join(", ", sentQuestions),
                            string.Join(", ", answeredQuestions));
                    }
                }
                else
                {
                    _log.Warning("Client for id [{0}] not found. Discarding response.", msg.Id);
                }
                break;
            }

            case Udp.CommandFailed { Cmd: Udp.Send send } cmdFailed:
            {
                try
                {
                    var msg = DnsProtocol.Message.Parse(send.Payload.ToArray());
                    if (_inflightRequests.TryGetValue(msg.Id, out var inFlight))
                    {
                        inFlight.ReplyTo.Tell(new Status.Failure(new Exception("Send failed to nameserver")));
                        _inflightRequests.Remove(msg.Id);
                    }
                }
                catch
                {
                    _log.Warning("DNS client failed to send {0}", cmdFailed.Cmd);
                }

                break;
            }
            case Udp.CommandFailed cmdFailed:
                _log.Warning("DNS client failed to send {0}", cmdFailed.Cmd);
                break;
            case TcpDropped _:
            case Tcp.Aborted _:
                _log.Warning("TCP client failed, clearing inflight resolves which were being resolved by TCP");
                var tcpRequests = _inflightRequests
                    .Where(kv => kv.Value.TcpRequest)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                
                foreach (var inFlight in tcpRequests.Values)
                {
                    inFlight.ReplyTo.Tell(new Status.Failure(new Exception("TCP connection to nameserver failed")));    
                }
                _inflightRequests = _inflightRequests
                    .Where(kv => !tcpRequests.ContainsKey(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                break;
            case Udp.Unbind _:
                Sender.Tell(Udp.Unbind.Instance);
                break;
                
            case Udp.Unbound _:
                Context.Stop(Self);
                break;
            case IO.Dns.Resolve r:
                HandleLegacyResolveRequest(r);
                break;
            default:
                Unhandled(message);
                break;
        }
    }

    /// <summary>
    /// Handle both A and AAAA record types for a single Resolve request
    /// </summary>
    private void HandleLegacyResolveRequest(IO.Dns.Resolve request)
    {
        // First try to get from cache
        var answer = cache.GetCached(request.Name);
        if (answer != null)
        {
            Sender.Tell(answer);
            return;
        }
        
        _log.Debug("Resolving both A and AAAA records for [{0}]", request.Name);
        
        // Generate unique IDs for both requests
        var idA = NewQueryId();
        var idAaaa = NewQueryId();
        //make sure both IDs are unique 
        while (idAaaa == idA)
        {
            idAaaa = NewQueryId();
        }
        SendDnsQuestion(idA, request.Name, DnsProtocol.RecordType.A, idAaaa);
        SendDnsQuestion(idAaaa, request.Name, DnsProtocol.RecordType.Aaaa, idA);
    }
    
    /// <summary>
    /// Send a DNS question to the configured nameserver
    /// </summary>
    private void SendDnsQuestion(short id, string name, DnsProtocol.RecordType recordType, short? linkedId = null)
    {
        var msg = CreateMessage(name, id, recordType);
        _inflightRequests[id] = new InFlightRequest(Sender, msg, false, linkedId);
        _log.Debug("Message [{0}] to [{1}]: [{2}]", id, nameserver, msg);
        
        var data = ByteString.FromBytes(msg.Write());
        _udpSocket.Tell(new Udp.Send(data, nameserver, Udp.NoAck.Instance));
    }
    

    private void HandleQuestion(string name, DnsProtocol.RecordType recordType)
    {
        var answer = cache.GetCached(name);
        if (answer != null)
        {
            Sender.Tell(answer);
            return;
        }

        SendDnsQuestion(NewQueryId(), name, recordType);
    }
    
    internal virtual DnsProtocol.Message CreateMessage(string name, short id, DnsProtocol.RecordType recordType)
    {
        var question = new DnsProtocol.Question(name, recordType, DnsProtocol.RecordClass.In);
        return new DnsProtocol.Message(
            id,
            new DnsProtocol.MessageFlags(),
            ImmutableList.Create(question));
    }

    private IEnumerable<(string Name, DnsProtocol.RecordType Type)> WithAndWithoutTrailingDots(DnsProtocol.Question question)
    {
        yield return (question.Name, question.Type);
            
        if (question.Name.EndsWith("."))
            yield return (question.Name.Substring(0, question.Name.Length - 1), question.Type);
        else
            yield return (question.Name + ".", question.Type);
    }

    private IActorRef CreateTcpClient()
    {
        var backoffOptions = Backoff.OnFailure(
            childProps: Props.Create(() => new TcpDnsClient(_tcpManager, nameserver, Self)),
            childName: "tcpDnsClient",
            minBackoff: TimeSpan.FromMilliseconds(10),
            maxBackoff: TimeSpan.FromSeconds(20),
            randomFactor: 0.1, 
            maxNrOfRetries: Int32.MinValue);
            
        return Context.ActorOf(
            BackoffSupervisor.Props(backoffOptions),
            "tcpDnsClientSupervisor");
    }   
    /// <summary>
    /// Generate random unique query ID
    /// </summary>
    /// <returns></returns>
    private short NewQueryId()
    {
        var r = (short)Random.Next(short.MinValue, short.MaxValue);
        while (_inflightRequests.ContainsKey(r))
        {
            r++;
        }
        return r;
    } 
    
    /// <summary>
    /// Determine if need to cache DNS response and for how long
    /// </summary>
    /// <param name="answer">Dns message response</param>
    /// <param name="ttl">Store item in cache for this amount of ms</param>
    /// <returns>false if positive-ttl = false, or true otherwise</returns>
    bool GetCacheTtl(DnsProtocol.Message answer, out long ttl)
    {
        switch (_positiveTtl)
        {
            case PositiveTtl.Never:
                ttl = long.MinValue;
                return false;
            case PositiveTtl.TtlTimeSpan ts:
                ttl = (long)ts.TimeSpan.TotalMilliseconds;
                return true;
            default:
                ttl = DnsProtocol.Message.MinTtl(answer);
                return true;

        }
    }
}
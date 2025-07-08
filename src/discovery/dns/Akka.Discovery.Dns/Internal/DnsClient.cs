using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.IO;
using Akka.Pattern;

namespace Akka.Discovery.Dns.Internal;

public class AsyncDnsExt : DnsExt
{
    public AsyncDnsExt(ExtendedActorSystem system) : base(system)
    {
        _system = system;
    }
    private readonly ExtendedActorSystem _system;
    private IActorRef? _manager; 
    public override IActorRef Manager 
    {
        get
        {
            // base implementation doesn't respect custom provider/manager settings, perhpaps on purpouse 
            return _manager = _manager ??  _system.SystemActorOf(Props.Create(Provider.ManagerClass, this).WithDeploy(Deploy.Local).WithDispatcher(Settings.Dispatcher)
                .WithDeploy(Deploy.Local)
                .WithDispatcher(Settings.Dispatcher));
        }
    }
}

public class AsyncDnsProvider : IDnsProvider
{
    private readonly DnsBase _cache = new SimpleDnsCache();

    /// <summary>
    /// TBD
    /// </summary>
    public DnsBase Cache => _cache;

    /// <summary>
    /// TBD
    /// </summary>
    public Type ActorClass => typeof (DnsClient);

    /// <summary>
    /// TBD
    /// </summary>
    public Type ManagerClass => typeof (DnsClient);
}

/// <summary>
/// DNS client actor for resolving DNS queries, including SRV records.
/// This is an internal implementation for the Akka.Discovery.Dns service.
/// </summary>
internal class DnsClient : UntypedActorWithStash
{
    #region Messages

    /// <summary>
    /// Base class for DNS questions
    /// </summary>
    public abstract record DnsQuestion(short Id);

    /// <summary>
    /// Question for SRV records
    /// </summary>
    public sealed record SrvQuestion(short Id, string Name) : DnsQuestion(Id);

    /// <summary>
    /// Question for A records (IPv4)
    /// </summary>
    public sealed record Question4(short Id, string Name) : DnsQuestion(Id);

    /// <summary>
    /// Question for AAAA records (IPv6)
    /// </summary>
    public sealed record Question6(short Id, string Name) : DnsQuestion(Id);

    /// <summary>
    /// DNS answer containing resource records
    /// </summary>
    public sealed record Answer
    {
        public short Id { get; }
        public ImmutableArray<ResourceRecord> Records { get; }
        public ImmutableArray<ResourceRecord> AdditionalRecords { get; }

        public Answer(short id, IEnumerable<ResourceRecord>? records = null, IEnumerable<ResourceRecord>? additionalRecords = null)
        {
            Id = id;
            Records = records?.ToImmutableArray() ?? ImmutableArray<ResourceRecord>.Empty;
            AdditionalRecords = additionalRecords?.ToImmutableArray() ?? ImmutableArray<ResourceRecord>.Empty;
        }
    }

    /// <summary>
    /// Request to drop a pending DNS question
    /// </summary>
    public sealed record DropRequest(DnsQuestion Question);

    /// <summary>
    /// Notification that a request has been dropped
    /// </summary>
    public sealed record Dropped(short Id);

    /// <summary>
    /// Internal message for UDP DNS answers
    /// </summary>
    private sealed record UdpAnswer
    {
        public ImmutableArray<DnsProtocol.Question> Questions { get; }
        public Answer Content { get; }

        public UdpAnswer(IEnumerable<DnsProtocol.Question> questions, Answer content)
        {
            Questions = questions.ToImmutableArray();
            Content = content;
        }
    }

    /// <summary>
    /// Message indicating TCP connection dropped
    /// </summary>
    public static readonly object TcpDropped = new object();

    #endregion

    private readonly EndPoint _nameserver;
    private readonly ILoggingAdapter _log;
    private readonly IActorRef _tcpManager;
    private readonly IActorRef _udpManager;
    private readonly IStash _stash;
        
    // Tracks in-flight DNS requests
    private Dictionary<short, InFlightRequest> _inflightRequests = new Dictionary<short, InFlightRequest>();
    private IActorRef _tcpDnsClient;
    private IActorRef? _udpSocket;


    /// <summary>
    /// Information about an in-flight DNS request
    /// </summary>
    private class InFlightRequest
    {
        public IActorRef ReplyTo { get; }
        public DnsProtocol.Message Message { get; }
        public bool TcpRequest { get; set; }

        public InFlightRequest(IActorRef replyTo, DnsProtocol.Message message, bool tcpRequest = false)
        {
            ReplyTo = replyTo;
            Message = message;
            TcpRequest = tcpRequest;
        }
    }

    static EndPoint ParseEndPoint(string endpoint)
    {
        var parts = endpoint.Split(':');
        var ip = IPAddress.Parse(parts[0]);
        var port = 0;
        if (parts.Length > 1)
        {
            port = int.Parse(parts[1]);
        }

        return new IPEndPoint(ip, port);
    }
    
    public DnsClient(AsyncDnsExt ext)
    {
        _log = Context.GetLogger();
        var ns  = ext.Settings.ResolverConfig.GetString(AsyncDnsResolerOptions.NameserversPath) ?? throw new ConfigurationException("nameservers config was empty");
        _nameserver = ParseEndPoint(ns);
        _udpManager = Akka.IO.Udp.Instance.Apply(Context.System).Manager;
        _tcpManager = Akka.IO.Tcp.Manager(Context.System);
        _stash = Context.CreateStash(typeof(DnsClient));
        _log.Log(LogLevel.DebugLevel, "Constructed!");
    }

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
            case Question4 _:
            case Question6 _:
            case SrvQuestion _:
                _stash.Stash();
                break;
        }
    }

    private void Ready(object message)
    {
        _log.Debug("Received message:[{0}]", message.GetType());
        switch (message)
        {
            case DropRequest dropRequest:
                HandleDropRequest(dropRequest);
                break;
                
            case Question4 question:
                HandleQuestion(question.Id, question.Name, DnsProtocol.RecordType.A, Sender);
                break;
                
            case Question6 question:
                HandleQuestion(question.Id, question.Name, DnsProtocol.RecordType.Aaaa, Sender);
                break;
                
            case SrvQuestion question:
                HandleQuestion(question.Id, question.Name, DnsProtocol.RecordType.Srv, Sender);
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
                        var records = msg.Flags.ResponseCode == DnsProtocol.ResponseCode.Success
                            ? msg.AnswerRecords : ImmutableList<ResourceRecord>.Empty;
                        var additionalRecs = msg.Flags.ResponseCode == DnsProtocol.ResponseCode.Success
                            ? msg.AdditionalRecords : ImmutableList<ResourceRecord>.Empty;
                            
                        Self.Tell(new UdpAnswer(msg.Questions, new Answer(msg.Id, records, additionalRecs)));
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error processing DNS response");
                }
                break;
                
            case UdpAnswer udpAnswer:
                if (_inflightRequests.TryGetValue(udpAnswer.Content.Id, out var request))
                {
                    var sentQuestions = request.Message.Questions.SelectMany(WithAndWithoutTrailingDots).ToArray().ToImmutableArray();
                    var answeredQuestions = udpAnswer.Questions.SelectMany(WithAndWithoutTrailingDots).ToImmutableArray();

                    if (answeredQuestions.Length == 0 || sentQuestions.Intersect(answeredQuestions).Any())
                    {
                        request.ReplyTo.Tell(udpAnswer.Content);
                        _inflightRequests.Remove(udpAnswer.Content.Id);
                    }
                    else
                    {
                        _log.Warning("Martian DNS response for id [{0}]. Expected names [{1}], received names [{2}]. Discarding response",
                            udpAnswer.Content.Id,
                            string.Join(", ", sentQuestions),
                            string.Join(", ", answeredQuestions));
                    }
                }
                else
                {
                    _log.Debug("Client for id [{0}] not found. Discarding response.", udpAnswer.Content.Id);
                }
                break;

            case Answer answer:
            {
                if (_inflightRequests.TryGetValue(answer.Id, out var inFlight))
                {
                    inFlight.ReplyTo.Tell(answer);
                    _inflightRequests.Remove(answer.Id);
                }
                else
                {
                    _log.Debug("Client for id [{0}] not found. Discarding response.", answer.Id);
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
                
            case Tcp.Aborted _:
                _log.Warning("TCP client failed, clearing inflight resolves which were being resolved by TCP");
                _inflightRequests = _inflightRequests.Where(kv => !kv.Value.TcpRequest)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                break;
                
            case Udp.Unbind _:
                Sender.Tell(Udp.Unbind.Instance);
                break;
                
            case Udp.Unbound _:
                Context.Stop(Self);
                break;
        }
    }

    private void HandleQuestion(short id, string name, DnsProtocol.RecordType recordType, IActorRef sender)
    {
        if (_inflightRequests.ContainsKey(id))
        {
            _log.Warning("DNS transaction ID collision encountered for ID [{0}], ignoring. This likely indicates a bug.",
                id);
            return;
        }

        _log.Debug("Resolving [{0}] ({1})", name, recordType);

        var msg = CreateMessage(name, id, recordType);
        _inflightRequests[id] = new InFlightRequest(sender, msg);
        _log.Debug("Message [{0}] to [{1}]: [{2}]", id, _nameserver, msg);

        // Send via bound UDP socket - assumes Context has been switched to Ready state with socket as Sender
        
        var data = ByteString.FromBytes(msg.Write());
        _udpSocket.Tell(new Udp.Send( data, _nameserver, Udp.NoAck.Instance ));
    }

    private void HandleDropRequest(DropRequest dropRequest)
    {
        var id = dropRequest.Question.Id;
        if (_inflightRequests.TryGetValue(id, out var inFlight))
        {
            var sentQuestions = inFlight.Message.Questions.Select(q => new { q.Name, q.Type }).ToList();
                
            string expectedName = null;
            DnsProtocol.RecordType expectedType = DnsProtocol.RecordType.A;
                
            switch (dropRequest.Question)
            {
                case Question4 q4:
                    expectedName = q4.Name;
                    expectedType = DnsProtocol.RecordType.A;
                    break;
                case Question6 q6:
                    expectedName = q6.Name;
                    expectedType = DnsProtocol.RecordType.Aaaa;
                    break;
                case SrvQuestion srv:
                    expectedName = srv.Name;
                    expectedType = DnsProtocol.RecordType.Srv;
                    break;
            }
                
            if (sentQuestions.Any(q => q.Name == expectedName && q.Type == expectedType))
            {
                _log.Debug("Dropping request [{0}]", id);
                _inflightRequests.Remove(id);
                Sender.Tell(new Dropped(id));
            }
            else if (_log.IsInfoEnabled)
            {
                _log.Info("Requested to drop request for id [{0}] expecting [{1}/{2}] but found requests for [{3}]... ignoring drop request",
                    id, 
                    expectedName, 
                    expectedType,
                    string.Join(", ", sentQuestions.Select(q => $"{q.Name}/{q.Type}")));
            }
        }
        else
        {
            Sender.Tell(new Dropped(id));
        }
    }

    private DnsProtocol.Message CreateMessage(string name, short id, DnsProtocol.RecordType recordType)
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
            childProps: Props.Create(() => new TcpDnsClient(_tcpManager, _nameserver, Self)),
            childName: "tcpDnsClient",
            minBackoff: TimeSpan.FromMilliseconds(10),
            maxBackoff: TimeSpan.FromSeconds(20),
            randomFactor: 0.1, 
            maxNrOfRetries: Int32.MinValue);
            
        return Context.ActorOf(
            BackoffSupervisor.Props(backoffOptions),
            "tcpDnsClientSupervisor");
    }
}
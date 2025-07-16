using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;
using Akka.Event;
using Akka.IO;

namespace Akka.Discovery.Dns.Internal;

/// <summary>
/// TCP DNS client actor for handling DNS requests over TCP.
/// Used as a fallback when UDP responses are truncated.
/// </summary>
internal class TcpDnsClient : UntypedActor
{
    private readonly EndPoint _nameserver;
    private readonly IActorRef _parent;
    private readonly ILoggingAdapter _log;
    private readonly IActorRef _tcpManager;

    private IActorRef _connection;
    private byte[] _readBuffer = new byte[2048]; // Buffer for reading DNS responses
    private int _expectedLength = -1; // Expected length of current DNS response
    private int _currentPosition = 0; // Current position in the buffer

    // Pending requests that need to be sent once connection is established
    private Queue<DnsProtocol.Message> _pendingRequests = new Queue<DnsProtocol.Message>();

    public TcpDnsClient(IActorRef tcpManager, EndPoint nameserver, IActorRef parent)
    {
        _tcpManager = tcpManager;
        _nameserver = nameserver;
        _parent = parent;
        _log = Context.GetLogger();
    }

    protected override void PreStart()
    {
        // Connect to the DNS server over TCP
        _tcpManager.Tell(new Tcp.Connect(_nameserver));
    }

    protected override void OnReceive(object message)
    {
        switch (message)
        {
            case Tcp.Connected connected:
                _log.Debug("Connected to DNS server at [{0}]", connected.RemoteAddress);
                _connection = Sender;
                _connection.Tell(new Tcp.Register(Self));
                    
                // Send any pending requests
                while (_pendingRequests.Count > 0)
                {
                    SendMessage(_pendingRequests.Dequeue());
                }
                break;
                
            case Tcp.CommandFailed failed when failed.Cmd is Tcp.Connect:
                _log.Warning("Failed to connect to DNS server: {0}", failed);
                _parent.Tell(AsyncDnsClient.TcpDropped);
                Context.Stop(Self);
                break;
                
            case Tcp.Received received:
                ProcessReceivedData(received.Data.ToArray());
                break;
                
            case Tcp.ConnectionClosed _:
                _log.Debug("Connection to DNS server closed");
                _parent.Tell(AsyncDnsClient.TcpDropped);
                Context.Stop(Self);
                break;
                
            case DnsProtocol.Message msg:
                if (_connection != null)
                {
                    SendMessage(msg);
                }
                else
                {
                    // Store message to send after connection is established
                    _pendingRequests.Enqueue(msg);
                }
                break;
                
            case Status.Failure failure:
                _log.Error(failure.Cause, "TCP DNS client failure");
                _parent.Tell(AsyncDnsClient.TcpDropped);
                Context.Stop(Self);
                break;
        }
    }

    private void SendMessage(DnsProtocol.Message message)
    {
        try
        {
            // For TCP DNS, we need to prefix the message with a 2-byte length field
            byte[] dnsMessage = message.Write();
            byte[] lengthPrefixed = new byte[dnsMessage.Length + 2];
                
            // Add length prefix in network byte order (big endian)
            lengthPrefixed[0] = (byte)((dnsMessage.Length >> 8) & 0xFF);
            lengthPrefixed[1] = (byte)(dnsMessage.Length & 0xFF);
                
            // Copy the DNS message after the length prefix
            Array.Copy(dnsMessage, 0, lengthPrefixed, 2, dnsMessage.Length);
                
            // Send the message
            _connection.Tell(Tcp.Write.Create(ByteString.FromBytes(lengthPrefixed)));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send DNS message over TCP");
            _parent.Tell(new Status.Failure(ex));
        }
    }

    private void ProcessReceivedData(byte[] data)
    {
        try
        {
            // Copy received data to the buffer
            Array.Copy(data, 0, _readBuffer, _currentPosition, data.Length);
            _currentPosition += data.Length;
                
            // Process all complete messages in the buffer
            while (_currentPosition >= 2)
            {
                if (_expectedLength == -1)
                {
                    // Extract the length prefix
                    _expectedLength = (_readBuffer[0] << 8) | _readBuffer[1];
                        
                    if (_expectedLength <= 0)
                    {
                        _log.Warning("Invalid DNS message length: {0}", _expectedLength);
                        ResetBuffer();
                        return;
                    }
                }
                    
                // Check if we have a complete message
                if (_currentPosition >= _expectedLength + 2)
                {
                    // Extract the DNS message (skipping the length prefix)
                    var messageData = new byte[_expectedLength];
                    Array.Copy(_readBuffer, 2, messageData, 0, _expectedLength);
                        
                    // Parse and process the message
                    var dnsMessage = DnsProtocol.Message.Parse(messageData);
                    _log.Debug("Received DNS response over TCP: {0}", dnsMessage);
                        
                    // Get resource records based on the response code
                    // var records = dnsMessage.Flags.ResponseCode == DnsProtocol.ResponseCode.Success 
                    //     ? dnsMessage.AnswerRecords : Array.Empty<ResourceRecord>().ToImmutableList();
                    // var additionalRecs = dnsMessage.Flags.ResponseCode == DnsProtocol.ResponseCode.Success 
                    //     ? dnsMessage.AdditionalRecords : Array.Empty<ResourceRecord>().ToImmutableList();
                        
                    // Forward the answer to the parent
                    // _parent.Tell(new DnsClient.Answer(dnsMessage.Id, dnsMessage.FirstQuestionName, records, additionalRecs));
                    // _parent.Tell(new DnsClient.Answer(dnsMessage.Id, dnsMessage.FirstQuestionName, records, additionalRecs));
                    _parent.Tell(dnsMessage);
                    
                    // Remove the processed message from the buffer
                    var remaining = _currentPosition - (_expectedLength + 2);
                    if (remaining > 0)
                    {
                        Array.Copy(_readBuffer, _expectedLength + 2, _readBuffer, 0, remaining);
                    }
                    _currentPosition = remaining;
                    _expectedLength = -1;
                }
                else
                {
                    // Need more data for a complete message
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to process DNS response from TCP");
            ResetBuffer();
        }
    }

    private void ResetBuffer()
    {
        _currentPosition = 0;
        _expectedLength = -1;
    }

    protected override void PostStop()
    {
        // Close the connection when the actor stops
        if (_connection != null)
        {
            _connection.Tell(Tcp.Close.Instance);
        }
    }
}
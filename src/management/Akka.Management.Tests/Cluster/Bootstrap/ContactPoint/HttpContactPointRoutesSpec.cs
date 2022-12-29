﻿//-----------------------------------------------------------------------
// <copyright file="HttpContactPointRoutesSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Http.Dsl;
using Akka.Management.Cluster.Bootstrap;
using Akka.Management.Cluster.Bootstrap.ContactPoint;
using Akka.Management.Dsl;
using Ceen;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using static Akka.Management.Cluster.Bootstrap.ContactPoint.HttpBootstrapJsonProtocol;

namespace Akka.Management.Tests.Cluster.Bootstrap.ContactPoint
{
    public class HttpContactPointRoutesSpec : TestKit.Xunit2.TestKit
    {
        private static readonly Config Config = ConfigurationFactory.ParseString(@"
            akka.actor.provider = cluster
            akka.remote.dot-netty.tcp.hostname = ""127.0.0.1""
            akka.remote.dot-netty.tcp.port = 0")
            .WithFallback(ClusterBootstrap.DefaultConfiguration())
            .WithFallback(AkkaManagementProvider.DefaultConfiguration());

        private readonly ClusterBootstrapSettings _settings;
        private readonly HttpClusterBootstrapRoutes _httpBootstrap;

        public HttpContactPointRoutesSpec(ITestOutputHelper helper) 
            : base(Config, nameof(HttpContactPointRoutesSpec), helper)
        {
            _settings = ClusterBootstrapSettings.Create(Sys.Settings.Config, Sys.Log);
            _httpBootstrap = new HttpClusterBootstrapRoutes(_settings);
        }

        [Fact(DisplayName = "Http Bootstrap routes should empty list if node is not part of a cluster")]
        public async Task EmptyListIfNotPartOfCluster()
        {
            var context = new DefaultHttpContext();
            context.FakeRequest.Method = "GET";
            context.FakeRequest.Path = ClusterBootstrapRequests.BootstrapSeedNodes("").ToString();
            
            var requestContext = new AkkaHttpContext(Sys, context);
            var handled = false;
            foreach (var (path, handler) in _httpBootstrap.Routes)
            {
                if (path == context.Request.Path)
                {
                    if (await handler.HandleAsync(requestContext))
                    {
                        handled = true;
                        var response = (FakeResponse)context.Response;
                        response.Response.Should().Contain("\"Nodes\":[]");
                    }
                }
            }

            handled.Should().BeTrue("At least one handler has to handle the request");
        }

        [Fact( 
            Skip = "Extremely racy in CI/CD",
            DisplayName = "Http Bootstrap routes should include seed nodes when part of a cluster")]
        public async Task IncludeSeedsWhenPartOfCluster()
        {
            var tcs = new TaskCompletionSource<Done>();
            var cluster = Akka.Cluster.Cluster.Get(Sys);
            cluster.RegisterOnMemberUp(() =>
            {
                tcs.SetResult(Done.Instance);
            });
            cluster.Join(cluster.SelfAddress);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await Task.WhenAny(Task.Delay(Timeout.Infinite, cts.Token), tcs.Task);
            if (cts.IsCancellationRequested)
                throw new TimeoutException("Cluster failed to form");
            cts.Dispose();

            var context = new DefaultHttpContext
            {
                FakeRequest =
                {
                    Method = "GET",
                    Path = ClusterBootstrapRequests.BootstrapSeedNodes("")
                }
            };

            var requestContext = new AkkaHttpContext(Sys, context);
            var handled = false;
            foreach (var (path, handler) in _httpBootstrap.Routes)
            {
                if (path == context.Request.Path)
                {
                    if (await handler.HandleAsync(requestContext))
                    {
                        handled = true;
                        var response = (FakeResponse)context.Response;
                        var nodes = JsonConvert.DeserializeObject<SeedNodes>(response.Response);
                        var seedNodes = nodes.Nodes.Select(n => n.Node).ToList();
                        seedNodes.Contains(cluster.SelfAddress).Should()
                            .BeTrue(
                                "Seed nodes should contain self address but it does not. Self address: [{0}], seed nodes: [{1}], response string: [{2}]",
                                cluster.SelfAddress,
                                string.Join(", ", seedNodes),
                                response.Response);
                    }
                }
            }

            handled.Should().BeTrue("At least one handler has to handle the request");
        }
    }

    internal class DefaultHttpContext : IHttpContext
    {
        public Task LogMessageAsync(LogLevel level, string message, Exception ex)
        {
            throw new NotImplementedException();
        }

        // ReSharper disable UnassignedGetOnlyAutoProperty
        public FakeRequest FakeRequest { get; } = new FakeRequest();
        public IHttpRequest Request => FakeRequest;
        public IHttpResponse Response { get; } = new FakeResponse();
        public IStorageCreator Storage { get; }
        public IDictionary<string, string> Session { get; set; }
        public IDictionary<string, string> LogData { get; }
        public ILoadedModuleInfo LoadedModules { get; }
    }

    internal class FakeRequest : IHttpRequest
    {
        public void PushHandlerOnStack(IHttpModule handler)
        {
            throw new NotImplementedException();
        }

        public void RequireHandler(IEnumerable<RequireHandlerAttribute> attributes)
        {
            throw new NotImplementedException();
        }

        public void RequireHandler(Type handler, bool allowderived = true)
        {
            throw new NotImplementedException();
        }

        public void ResetProcessingTimeout()
        {
            throw new NotImplementedException();
        }

        public void ThrowIfTimeout()
        {
            throw new NotImplementedException();
        }

        // ReSharper disable UnassignedGetOnlyAutoProperty
        public string? RawHttpRequestLine { get; }
        public string? Method { get; set; }
        public string? Path { get; set; }
        public string? OriginalPath { get; }
        public string? RawQueryString { get; }
        public IDictionary<string, string>? QueryString { get; }
        public IDictionary<string, string>? Headers { get; }
        public IDictionary<string, string>? Form { get; }
        public IDictionary<string, string>? Cookies { get; }
        public IList<IMultipartItem>? Files { get; }
        public string? HttpVersion { get; set; }
        public string? UserID { get; set; }
        public string? SessionID { get; set; }
        public SslProtocols SslProtocol { get; }
        public EndPoint? RemoteEndPoint { get; }
        public X509Certificate? ClientCertificate { get; }
        public string? LogConnectionID { get; }
        public string? LogRequestID { get; }
        public Stream? Body { get; set; }
        public string? ContentType { get; }
        public int ContentLength { get; }
        public string? Hostname { get; }
        public IDictionary<string, object>? RequestState { get; }
        public IEnumerable<IHttpModule>? HandlerStack { get; }
        public CancellationToken TimeoutCancellationToken { get; }
        public bool IsConnected { get; }
        public DateTime RequestProcessingStarted { get; }
        // ReSharper restore UnassignedGetOnlyAutoProperty
    }

    internal class FakeResponse : IHttpResponse
    {
        public string Response { get; private set; }
        
        public IResponseCookie AddCookie(string name, string value, string path = null, string domain = null, DateTime? expires = null,
            long maxage = -1, bool secure = false, bool httponly = false, string samesite = null)
        {
            throw new NotImplementedException();
        }

        public IResponseCookie AddCookie(string name, string value, string path = null, string domain = null, DateTime? expires = null,
            long maxage = -1, bool secure = false, bool httponly = false)
        {
            throw new NotImplementedException();
        }

        public void AddHeader(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void InternalRedirect(string path)
        {
            throw new NotImplementedException();
        }

        public Task FlushHeadersAsync()
        {
            throw new NotImplementedException();
        }

        public async Task WriteAllAsync(Stream data, string? contenttype = null)
        {
            using var reader = new StreamReader(data);
            Response = await reader.ReadToEndAsync();
        }

        public Task WriteAllAsync(byte[] data, string? contenttype = null)
        {
            Response = Encoding.UTF8.GetString(data);
            return Task.CompletedTask;
        }

        public Task WriteAllAsync(string data, string? contenttype = null)
        {
            Response = data;
            return Task.CompletedTask;
        }

        public Task WriteAllAsync(string data, Encoding encoding, string? contenttype = null)
        {
            Response = data;
            return Task.CompletedTask;
        }

        public Task WriteAllJsonAsync(string data)
        {
            Response = data;
            return Task.CompletedTask;
        }

        public void Redirect(string newurl)
        {
            throw new NotImplementedException();
        }

        public void SetNonCacheable()
        {
            throw new NotImplementedException();
        }

        public void SetExpires(TimeSpan duration, bool isPublic = true)
        {
            throw new NotImplementedException();
        }

        public void SetExpires(DateTime until, bool isPublic = true)
        {
            throw new NotImplementedException();
        }

        public Stream GetResponseStream()
        {
            throw new NotImplementedException();
        }

        // ReSharper disable UnassignedGetOnlyAutoProperty
        public string? HttpVersion { get; set; }
        public Ceen.HttpStatusCode StatusCode { get; set; }
        public string? StatusMessage { get; set; }
        public bool HasSentHeaders { get; }
        public IDictionary<string, string>? Headers { get; }
        public IList<IResponseCookie>? Cookies { get; }
        public bool IsRedirectingInternally { get; }
        public string? ContentType { get; set; }
        public long ContentLength { get; set; }
        public bool KeepAlive { get; set; }
        // ReSharper restore UnassignedGetOnlyAutoProperty
    }
}
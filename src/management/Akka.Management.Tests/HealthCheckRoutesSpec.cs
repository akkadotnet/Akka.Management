//-----------------------------------------------------------------------
// <copyright file="HealthCheckRoutesSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.Http.Extensions;
using Akka.Management.Dsl;
using Akka.Util;
using Ceen;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using HttpRequest = Akka.Http.Dsl.Model.HttpRequest;
using HttpStatusCode = Ceen.HttpStatusCode;

namespace Akka.Management.Tests
{
    public class HealthCheckRoutesSpec : TestKit.Xunit2.TestKit
    {
        private static Config Config = ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port = 0")
            .WithFallback(Akka.Http.Dsl.Http.DefaultConfig())
            .WithFallback(AkkaManagementProvider.DefaultConfiguration());
        
        public HealthCheckRoutesSpec(ITestOutputHelper helper) : base(Config, nameof(HealthCheckRoutesSpec), helper)
        { }
        
        [Fact(DisplayName = "Health check /ready endpoint should return 200 for right")]
        public async Task HealthCheckReadyReturn200ForRight()
        {
            var result = (RouteResult.Complete)
                await TestRoutes().Concat()(await Get("/ready"));
            result.Response.Status.Should().Be(HttpStatusCode.OK);
        }
        
        [Fact(DisplayName = "Health check /ready endpoint should return 500 for left")]
        public async Task HealthCheckReadyReturn500ForLeft()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(
                    readyResultValue: Task.FromResult((Either<string, Done>)new Left<string, Done>("com.someclass.MyCheck"))).Concat()(await Get("/ready"));
            result.Response.Status.Should().Be(HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Not Healthy: com.someclass.MyCheck");
        }
        
        [Fact(DisplayName = "Health check /ready endpoint should return 500 for fail")]
        public async Task HealthCheckReadyReturn500ForFail()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(
                    readyResultValue: Task.FromException<Either<string, Done>>(new Exception("darn it"))).Concat()(await Get("/ready"));
            result.Response.Status.Should().Be(HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Health Check Failed: darn it");
        }
        
        [Fact(DisplayName = "Health check /alive endpoint should return 200 for right")]
        public async Task HealthCheckAliveReturn200ForRight()
        {
            var result = (RouteResult.Complete)
                await TestRoutes().Concat()(await Get("/alive"));
            result.Response.Status.Should().Be(HttpStatusCode.OK);
        }
        
        [Fact(DisplayName = "Health check /alive endpoint should return 500 for left")]
        public async Task HealthCheckAliveReturn500ForLeft()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(
                    aliveResultValue: Task.FromResult(
                        (Either<string, Done>) new Left<string, Done>("com.someclass.MyCheck"))).Concat()(await Get("/alive"));
            result.Response.Status.Should().Be(HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Not Healthy: com.someclass.MyCheck");
        }
        
        [Fact(DisplayName = "Health check /alive endpoint should return 500 for fail")]
        public async Task HealthCheckAliveReturn500ForFail()
        {
            var result = (RouteResult.Complete)
                await TestRoutes(aliveResultValue: Task.FromException<Either<string, Done>>(new Exception("darn it"))).Concat()
                    (await Get("/alive"));
            result.Response.Status.Should().Be(HttpStatusCode.InternalServerError);
            result.Response.Entity.DataBytes.ToString().Should().Be("Health Check Failed: darn it");
        }

        private async Task<RequestContext> Get(string route)
        {
            var context = new DefaultHttpContext
            {
                FakeRequest =
                {
                    Method = "GET",
                    Path = route,
                    Body = new MemoryStream(),
                    HttpVersion = "HTTP/1.1"
                }
            };

            var request = await HttpRequest.CreateAsync(context.Request);
            
            return new RequestContext(request, Sys);
        }
        
        private Route[] TestRoutes(
            Task<Either<string, Done>> readyResultValue = null, 
            Task<Either<string, Done>> aliveResultValue = null)
        {
            return new TestHealthCheckRoutes((ExtendedActorSystem) Sys, readyResultValue, aliveResultValue)
                .Routes(new ManagementRouteProviderSettingsImpl(new Uri("http://whocares"), false));
        }

        private class TestHealthCheckRoutes : HealthCheckRoutes
        {
            internal override HealthChecks HealthChecks { get; }

            public TestHealthCheckRoutes(
                ExtendedActorSystem system,
                Task<Either<string, Done>> readyResultValue, 
                Task<Either<string, Done>> aliveResultValue) : base(system)
            {
                HealthChecks = new TestHealthChecks(readyResultValue, aliveResultValue);
            }
        }
        
        private class TestHealthChecks : HealthChecks
        {
            private readonly Task<Either<string, Done>> _readyResultValue;
            private readonly Task<Either<string, Done>> _aliveResultValue;

            public TestHealthChecks(
                Task<Either<string, Done>> readyResultValue, 
                Task<Either<string, Done>> aliveResultValue)
            {
                _readyResultValue =
                    readyResultValue ??
                    Task.FromResult((Either<string, Done>)new Right<string, Done>(Done.Instance));
                _aliveResultValue =
                    aliveResultValue ??
                    Task.FromResult((Either<string, Done>)new Right<string, Done>(Done.Instance));
            }

            public override Task<bool> Ready()
                => Task.FromResult(_readyResultValue.Result is Right<string, Done>);

            public override Task<Either<string, Done>> ReadyResult()
                => _readyResultValue;

            public override Task<bool> Alive()
                => Task.FromResult(_aliveResultValue.Result is Right<string, Done>);

            public override Task<Either<string, Done>> AliveResult()
                => _aliveResultValue;
        }
    }

    internal class DefaultHttpContext : IHttpContext
    {
        public Task LogMessageAsync(LogLevel level, string message, Exception ex)
        {
            throw new NotImplementedException();
        }

        public FakeRequest FakeRequest { get; } = new FakeRequest();
        public IHttpRequest Request => FakeRequest;
        public IHttpResponse Response { get; }
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

        public string RawHttpRequestLine { get; }
        public string Method { get; set; }
        public string Path { get; set; }
        public string OriginalPath { get; }
        public string RawQueryString { get; }
        public IDictionary<string, string> QueryString { get; }
        public IDictionary<string, string> Headers { get; }
        public IDictionary<string, string> Form { get; }
        public IDictionary<string, string> Cookies { get; }
        public IList<IMultipartItem> Files { get; }
        public string HttpVersion { get; set; }
        public string UserID { get; set; }
        public string SessionID { get; set; }
        public SslProtocols SslProtocol { get; }
        public EndPoint RemoteEndPoint { get; }
        public X509Certificate ClientCertificate { get; }
        public string LogConnectionID { get; }
        public string LogRequestID { get; }
        public Stream Body { get; set; }
        public string ContentType { get; }
        public int ContentLength { get; }
        public string Hostname { get; }
        public IDictionary<string, object> RequestState { get; }
        public IEnumerable<IHttpModule> HandlerStack { get; }
        public CancellationToken TimeoutCancellationToken { get; }
        public bool IsConnected { get; }
        public DateTime RequestProcessingStarted { get; }
    }

    internal class FakeResponse : IHttpResponse
    {
        public IResponseCookie AddCookie(string name, string value, string path = null, string domain = null, DateTime? expires = null,
            long maxage = -1, bool secure = false, bool httponly = false, string samesite = null)
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

        public Task WriteAllAsync(Stream data, string contenttype = null)
        {
            throw new NotImplementedException();
        }

        public Task WriteAllAsync(byte[] data, string contenttype = null)
        {
            throw new NotImplementedException();
        }

        public Task WriteAllAsync(string data, string contenttype = null)
        {
            throw new NotImplementedException();
        }

        public Task WriteAllAsync(string data, Encoding encoding, string contenttype = null)
        {
            throw new NotImplementedException();
        }

        public Task WriteAllJsonAsync(string data)
        {
            throw new NotImplementedException();
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

        public string HttpVersion { get; set; }
        public Ceen.HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public bool HasSentHeaders { get; }
        public IDictionary<string, string> Headers { get; }
        public IList<IResponseCookie> Cookies { get; }
        public bool IsRedirectingInternally { get; }
        public string ContentType { get; set; }
        public long ContentLength { get; set; }
        public bool KeepAlive { get; set; }
    }
}

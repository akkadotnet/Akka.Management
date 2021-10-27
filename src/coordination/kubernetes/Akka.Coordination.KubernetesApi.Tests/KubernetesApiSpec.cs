using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Coordination.KubernetesApi.Internal;
using Akka.Coordination.KubernetesApi.Models;
using Akka.Util;
using FluentAssertions;
using k8s.Models;
using Microsoft.Rest.Serialization;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Akka.Coordination.KubernetesApi.Tests
{
    public class KubernetesApiSpec : TestKit.Xunit2.TestKit
    {
        private readonly WireMockServer _wireMockServer;
        private readonly KubernetesSettings _settings;
        private readonly MockKubernetesApi _underTest;
        private const string LeaseName = "lease-1";
        private const string ApiPath = "/apis/akka.io/v1/namespaces/lease/leases";
        private const string LeaseApiPath = ApiPath + "/" + LeaseName;
        
        private static Config Config()
            => ConfigurationFactory.ParseString("akka.loglevel=DEBUG");
        
        public KubernetesApiSpec(ITestOutputHelper output) : base(Config(), nameof(KubernetesApiSpec), output)
        {
            _wireMockServer = WireMockServer.Start();
            
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", "localhost");
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_PORT", _wireMockServer.Ports[0].ToString());
            
            _settings = new KubernetesSettings(
                "",
                "",
                "KUBERNETES_SERVICE_HOST",
                "KUBERNETES_SERVICE_PORT",
                "lease",
                "",
                TimeSpan.FromMilliseconds(800),
                false);

            _underTest = new MockKubernetesApi(Sys, _settings);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _wireMockServer.Stop();
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", null);
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_PORT", null);
        }
        
        private class MockKubernetesApi : KubernetesApiImpl
        {
            public MockKubernetesApi(ActorSystem system, KubernetesSettings settings) : base(system, settings)
            {
            }

            // avoid touching slow CI filesystem
            protected override string? ReadConfigVarFromFileSystem(string path, string name) => null;
        }
        
        [Fact(DisplayName = "Kubernetes lease resource should be able to be created")]
        public async Task AbleToCreateLeaseResource()
        {
            const string version = "1234";
            try
            {
                _wireMockServer.Given(Request.Create().UsingPost().WithPath(ApiPath))
                    .RespondWith(Response.Create()
                        .WithStatusCode(201)
                        .WithHeader("Content-Type", "application/json")
                        .WithBodyAsJson(new LeaseCustomResource(
                            new V1ObjectMeta
                            {
                                Name = LeaseName,
                                NamespaceProperty = "akka-lease-tests",
                                ResourceVersion = version,
                                SelfLink = LeaseApiPath,
                                Uid = "c369949e-296c-11e9-9c62-16f8dd5735ba"
                            },
                            new LeaseSpec(owner: "", time: 1549439255948))));

                (await _underTest.RemoveLease(LeaseName)).Should().Be(Done.Instance);
                var leaseRecord = await _underTest.ReadOrCreateLeaseResource(LeaseName);
                leaseRecord.Owner.Should().Be(null);
                leaseRecord.Version.Should().NotBeNullOrEmpty();
                leaseRecord.Version.Should().Be(version);
            }
            finally
            {
                _wireMockServer.Reset();
            }
        }

        [Fact(DisplayName = "Kubernetes lease resource should update a lease successfully")]
        public async Task AbleToUpdateLease()
        {
            const string owner = "client1";
            const string updatedVersion = "3";
            var timestamp = DateTime.UtcNow;

            try
            {
                _wireMockServer.Given(Request.Create().UsingPut().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create()
                        .WithStatusCode(200)
                        .WithHeader("Content-Type", "application/json")
                        .WithCallback(request =>
                        {
                            var body = SafeJsonConvert.DeserializeObject<LeaseCustomResource>(request.Body);
                            var response =  new LeaseCustomResource(
                                new V1ObjectMeta
                                {
                                    Name = body.Metadata.Name,
                                    NamespaceProperty = "akka-lease-tests",
                                    ResourceVersion = updatedVersion,
                                    SelfLink = request.AbsolutePath,
                                    Uid = "c369949e-296c-11e9-9c62-16f8dd5735ba"
                                },
                                new LeaseSpec(owner: body.Spec.Owner, time: body.Spec.Time));
                            return new ResponseMessage
                            {
                                BodyDestination = null,
                                BodyData = new BodyData
                                {
                                    Encoding = null,
                                    DetectedBodyType = BodyType.Json,
                                    BodyAsJson = response,
                                    BodyAsJsonIndented = false
                                }
                            };
                        }));
                var response = await _underTest.UpdateLeaseResource(LeaseName, owner, "2", timestamp);
                response.Should().BeOfType<Right<LeaseResource, LeaseResource>>();
                var right = ((Right<LeaseResource, LeaseResource>)response).Value;
                right.Owner.Should().Be(owner);
                right.Version.Should().Be("3");
                right.Time.Should().Be((long)timestamp.TimeOfDay.TotalMilliseconds);
            }
            finally
            {
                _wireMockServer.Reset();
            }
        }

        [Fact(DisplayName = "Kubernetes lease resource should update a lease conflict")]
        public async Task ShouldUpdateLeaseConflict()
        {
            const string owner = "client1";
            const string conflictOwner = "client2";
            const string version = "2";
            const string updatedVersion = "3";
            var timestamp = DateTime.UtcNow;
            try
            {
                // Conflict
                _wireMockServer.Given(Request.Create().UsingPut().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Conflict));

                // Read to get version
                _wireMockServer.Given(Request.Create().UsingGet().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create()
                        .WithStatusCode(HttpStatusCode.OK)
                        .WithHeader("Content-Type", "application/json")
                        .WithBodyAsJson(new LeaseCustomResource(
                            new V1ObjectMeta
                            {
                                Name = LeaseName,
                                NamespaceProperty = "akka-lease-tests",
                                ResourceVersion = updatedVersion,
                                SelfLink = LeaseApiPath,
                                Uid = "c369949e-296c-11e9-9c62-16f8dd5735ba"
                            }, new LeaseSpec(owner: conflictOwner, time: timestamp))));

                var response = await _underTest.UpdateLeaseResource(LeaseName, owner, version, timestamp);
                response.Should().BeOfType<Left<LeaseResource, LeaseResource>>();
                var left = ((Left<LeaseResource, LeaseResource>)response).Value;
                left.Owner.Should().Be(conflictOwner);
                left.Version.Should().Be(updatedVersion);
                left.Time.Should().Be((long)timestamp.TimeOfDay.TotalMilliseconds);
            }
            finally
            {
                _wireMockServer.Reset();
            }

        }

        [Fact(DisplayName = "Kubernetes lease resource should remove lease")]
        public async Task ShouldRemoveLease()
        {
            try
            {
                _wireMockServer.Given(Request.Create().UsingDelete().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

                var response = await _underTest.RemoveLease(LeaseName);
                response.Should().Be(Done.Instance);
            }
            finally
            {
                _wireMockServer.Reset();
            }
        }

        [Fact(DisplayName = "Kubernetes lease resource should timeout on read lease")]
        public async Task ShouldTimeOutOnRead()
        {
            const string owner = "client1";
            const string version = "2";
            var timestamp = DateTime.UtcNow;

            try
            {
                _wireMockServer.Given(Request.Create().UsingGet().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create()
                        .WithDelay((int)(_settings.ApiServiceRequestTimeout.TotalMilliseconds * 2)) // time out
                        .WithStatusCode(HttpStatusCode.OK)
                        .WithHeader("Content-Type", "application/json")
                        .WithBodyAsJson(new LeaseCustomResource(
                            new V1ObjectMeta
                            {
                                Name = LeaseName,
                                NamespaceProperty = "akka-lease-tests",
                                ResourceVersion = version,
                                SelfLink = LeaseApiPath,
                                Uid = "c369949e-296c-11e9-9c62-16f8dd5735ba"
                            }, new LeaseSpec(owner: owner, time: timestamp))));
                var exception = await Record.ExceptionAsync( async () =>
                {
                    await _underTest.ReadOrCreateLeaseResource(LeaseName);
                });
                exception.Should().BeOfType<LeaseTimeoutException>();
                exception.Message.Should().StartWith($"Timed out reading lease {LeaseName}.");
            }
            finally
            {
                _wireMockServer.Reset();
            }
        }
        
        [Fact(DisplayName = "Kubernetes lease resource should timeout on create lease")]
        public async Task ShouldTimeOutOnCreate()
        {
            const string owner = "client1";
            const string version = "2";
            var timestamp = DateTime.UtcNow;

            try
            {
                _wireMockServer.Given(Request.Create().UsingGet().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NotFound));
                
                _wireMockServer.Given(Request.Create().UsingPost().WithPath(ApiPath))
                    .RespondWith(Response.Create()
                        .WithDelay((int)(_settings.ApiServiceRequestTimeout.TotalMilliseconds * 2)) // time out
                        .WithStatusCode(HttpStatusCode.OK)
                        .WithHeader("Content-Type", "application/json")
                        .WithBodyAsJson(new LeaseCustomResource(
                            new V1ObjectMeta
                            {
                                Name = LeaseName,
                                NamespaceProperty = "akka-lease-tests",
                                ResourceVersion = version,
                                SelfLink = LeaseApiPath,
                                Uid = "c369949e-296c-11e9-9c62-16f8dd5735ba"
                            }, new LeaseSpec(owner: owner, time: timestamp))));
                var exception = await Record.ExceptionAsync( async () =>
                {
                    await _underTest.ReadOrCreateLeaseResource(LeaseName);
                });
                exception.Should().BeOfType<LeaseTimeoutException>();
                exception.Message.Should().StartWith($"Timed out creating lease {LeaseName}.");
            }
            finally
            {
                _wireMockServer.Reset();
            }
        }
        
        [Fact(DisplayName = "Kubernetes lease resource should timeout on update lease")]
        public async Task ShouldTimeOutOnUpdate()
        {
            const string owner = "client1";
            const string version = "2";
            var timestamp = DateTime.UtcNow;

            try
            {
                _wireMockServer.Given(Request.Create().UsingPut().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create()
                        .WithDelay((int)(_settings.ApiServiceRequestTimeout.TotalMilliseconds * 2)) // time out
                        .WithStatusCode(HttpStatusCode.OK)
                        .WithHeader("Content-Type", "application/json")
                        .WithBodyAsJson(new LeaseCustomResource(
                            new V1ObjectMeta
                            {
                                Name = LeaseName,
                                NamespaceProperty = "akka-lease-tests",
                                ResourceVersion = version,
                                SelfLink = LeaseApiPath,
                                Uid = "c369949e-296c-11e9-9c62-16f8dd5735ba"
                            }, new LeaseSpec(owner: owner, time: timestamp))));
                var exception = await Record.ExceptionAsync( async () =>
                {
                    await _underTest.UpdateLeaseResource(LeaseName, owner, version);
                });
                exception.Should().BeOfType<LeaseTimeoutException>();
                exception.Message.Should().StartWith($"Timed out updating lease {LeaseName} to owner {owner}. It is not known if the update happened.");
            }
            finally
            {
                _wireMockServer.Reset();
            }
        }
        
        [Fact(DisplayName = "Kubernetes lease resource should timeout on remove lease")]
        public async Task ShouldTimeOutOnRemove()
        {
            try
            {
                _wireMockServer.Given(Request.Create().UsingDelete().WithPath(LeaseApiPath))
                    .RespondWith(Response.Create()
                        .WithDelay((int)(_settings.ApiServiceRequestTimeout.TotalMilliseconds * 2)) // time out
                        .WithStatusCode(HttpStatusCode.OK));
                
                var exception = await Record.ExceptionAsync( async () =>
                {
                    await _underTest.RemoveLease(LeaseName);
                });
                exception.Should().BeOfType<LeaseTimeoutException>();
                exception.Message.Should().StartWith($"Timed out removing lease {LeaseName}. It is not known if the remove happened.");
            }
            finally
            {
                _wireMockServer.Reset();
            }
        }              
    }
}
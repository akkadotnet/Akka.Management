//-----------------------------------------------------------------------
// <copyright file="ExtensionAutoStartSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Event;
using Akka.Management.Dsl;
using Xunit;

namespace Akka.Management.Tests
{
    /// <summary>
    /// Records every Info log line. It is configured through <c>akka.loggers</c>, so it is running
    /// before the startup extensions load and sees the logs they write during <see cref="ActorSystem"/> creation.
    /// </summary>
    internal sealed class InfoCapturingLogger : ReceiveActor
    {
        public static readonly ConcurrentQueue<(string Source, string Message)> Events = new();

        public InfoCapturingLogger()
        {
            Receive<InitializeLogger>(_ => Sender.Tell(new LoggerInitialized()));
            Receive<Info>(e => Events.Enqueue((e.LogSource, e.ToString())));
            ReceiveAny(_ => { });
        }
    }

    public sealed class ExtensionAutoStartSpec
    {
        private const string HoconProvider = "Akka.Management.Dsl.AkkaManagementProvider, Akka.Management";
        private const string AutoStartLog = "auto starting management";
        private const string BindingLog = "Binding Akka Management (HTTP) endpoint";

        private readonly ITestOutputHelper _output;

        public ExtensionAutoStartSpec(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(DisplayName = "Akka.Management should auto-start when its provider is passed through ExtensionsSetup without Akka.Hosting")]
        public Task Should_AutoStart_When_ProviderIsInExtensionsSetup()
            => AssertStartsExactlyOnceAsync("setup-only", inHocon: false, inSetup: true);

        [Fact(DisplayName = "Akka.Management should auto-start when its provider is listed in akka.extensions")]
        public Task Should_AutoStart_When_ProviderIsInHoconExtensions()
            => AssertStartsExactlyOnceAsync("hocon-only", inHocon: true, inSetup: false);

        [Fact(DisplayName = "Akka.Management should start exactly once when its provider is in both akka.extensions and ExtensionsSetup")]
        public Task Should_StartExactlyOnce_When_ProviderIsInHoconAndExtensionsSetup()
            => AssertStartsExactlyOnceAsync("hocon-and-setup", inHocon: true, inSetup: true);

        private async Task AssertStartsExactlyOnceAsync(string name, bool inHocon, bool inSetup)
        {
            var systemName = $"autostart-{name}-{Guid.NewGuid():N}";
            var httpPort = SocketUtil.TemporaryTcpAddress("127.0.0.1").Port;
            var config = ConfigurationFactory.ParseString($@"
                akka.loggers = [""Akka.Management.Tests.InfoCapturingLogger, Akka.Management.Tests""]
                akka.remote.dot-netty.tcp.port = 0
                akka.management.http.hostname = ""127.0.0.1""
                akka.management.http.port = {httpPort}
                akka.management.http.routes {{
                    test1 = ""Akka.Management.Tests.HttpManagementEndpointSpecRoutesDotNetDsl, Akka.Management.Tests""
                }}
                {(inHocon ? $@"akka.extensions = [""{HoconProvider}""]" : "")}");

            var setup = ActorSystemSetup.Create(BootstrapSetup.Create().WithConfig(config));
            if (inSetup)
                setup = setup.And(ExtensionsSetup.Create(new AkkaManagementProvider()));

            // The test never calls AkkaManagement.Start(): the HTTP endpoint only answers if auto-start ran.
            var system = ActorSystem.Create(systemName, setup);
            var kit = new Akka.TestKit.Xunit.TestKit(system, _output);
            try
            {
                using var client = new HttpClient();
                await kit.AwaitAssertAsync(async () =>
                {
                    var response = await client.GetAsync($"http://127.0.0.1:{httpPort}/dotnet");
                    response.EnsureSuccessStatusCode();
                    Assert.Equal("hello .NET Core", await response.Content.ReadAsStringAsync());
                }, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(200));

                var logs = InfoCapturingLogger.Events
                    .Where(e => e.Source.Contains(systemName))
                    .Select(e => e.Message)
                    .ToList();
                Assert.Single(logs, m => m.Contains(AutoStartLog));
                Assert.Single(logs, m => m.Contains(BindingLog));
            }
            finally
            {
                await kit.DisposeAsync();
            }
        }
    }
}

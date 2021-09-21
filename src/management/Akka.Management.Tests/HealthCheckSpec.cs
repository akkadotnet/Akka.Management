using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Management.Dsl;
using Akka.Management.Internal;
using Akka.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Akka.Management.HealthCheckSettings;

namespace Akka.Management.Tests
{
    public class HealthCheckSpec : TestKit.Xunit2.TestKit
    {
        private static readonly TE FailedCause = new TE();

        private static readonly TE CtxException = new TE();
            
        private class TE : Exception
        {
            public override string StackTrace => null;
        }
            
        public class Ok : IHealthCheck
        {
            public Ok(ActorSystem system) { }
            public Task<bool> Execute(CancellationToken token)
                => Task.FromResult(true);
        }
        
        public class False : IHealthCheck
        {
            public False(ActorSystem system) { }
            public Task<bool> Execute(CancellationToken token)
                => Task.FromResult(false);
        }
        
        public class Throws : IHealthCheck
        {
            public Throws(ActorSystem system) { }
            public Task<bool> Execute(CancellationToken token)
                => throw FailedCause;
        }
        
        public class NoArgsCtor : IHealthCheck
        {
            public Task<bool> Execute(CancellationToken token)
                => Task.FromResult(true);
        }
        
        public class InvalidCtor : IHealthCheck
        {
            public InvalidCtor(string cat) { }
            public Task<bool> Execute(CancellationToken token)
                => Task.FromResult(true);
        }
        
        public class Slow : IHealthCheck
        {
            public Slow(ActorSystem system) { }
            public async Task<bool> Execute(CancellationToken token)
            {
                await Task.Delay(20000, token);
                return false;
            }
        }
        
        public class Naughty : IHealthCheck
        {
            public Task<bool> Execute(CancellationToken token)
                => throw new Exception("bad");
        }
        
        public class WrongType { }
        
        public class CtorException : IHealthCheck
        {
            public CtorException(ActorSystem system)
            {
                throw CtxException;
            }
            
            public Task<bool> Execute(CancellationToken token)
                => Task.FromResult(true);
        }

        private readonly NamedHealthCheck OkCheck = new NamedHealthCheck(nameof(Ok), $"Akka.Management.Tests.HealthCheckSpec+{nameof(Ok)}, Akka.Management.Tests");
        private readonly NamedHealthCheck FalseCheck = new NamedHealthCheck(nameof(False), $"Akka.Management.Tests.HealthCheckSpec+{nameof(False)}, Akka.Management.Tests");
        private readonly NamedHealthCheck ThrowsCheck = new NamedHealthCheck(nameof(Throws), $"Akka.Management.Tests.HealthCheckSpec+{nameof(Throws)}, Akka.Management.Tests");
        private readonly NamedHealthCheck SlowCheck = new NamedHealthCheck(nameof(Slow), $"Akka.Management.Tests.HealthCheckSpec+{nameof(Slow)}, Akka.Management.Tests");
        private readonly NamedHealthCheck NoargsCtorCheck = new NamedHealthCheck(nameof(NoArgsCtor), $"Akka.Management.Tests.HealthCheckSpec+{nameof(NoArgsCtor)}, Akka.Management.Tests");
        private readonly NamedHealthCheck NaughtyCheck = new NamedHealthCheck(nameof(Naughty), $"Akka.Management.Tests.HealthCheckSpec+{nameof(Naughty)}, Akka.Management.Tests");
        private readonly NamedHealthCheck InvalidCtorCheck = new NamedHealthCheck(nameof(InvalidCtor), $"Akka.Management.Tests.HealthCheckSpec+{nameof(InvalidCtor)}, Akka.Management.Tests");
        private readonly NamedHealthCheck WrongTypeCheck = new NamedHealthCheck(nameof(WrongType), $"Akka.Management.Tests.HealthCheckSpec+{nameof(WrongType)}, Akka.Management.Tests");
        private readonly NamedHealthCheck DoesNotExist = new NamedHealthCheck("DoesNotExist", "Akka.Management.Tests.HealthCheckSpec+DoesNotExist, Akka.Management.Tests");
        private readonly NamedHealthCheck CtorExceptionCheck = new NamedHealthCheck(nameof(CtorException), $"Akka.Management.Tests.HealthCheckSpec+{nameof(CtorException)}, Akka.Management.Tests");

        private readonly ExtendedActorSystem _eas;
        
        public HealthCheckSpec(ITestOutputHelper helper) : base(nameof(HealthCheckSpec), helper)
        {
            _eas = (ExtendedActorSystem) Sys;
        }

        private HealthCheckSettings Settings(
            ImmutableList<NamedHealthCheck> readiness,
            ImmutableList<NamedHealthCheck> liveness)
            => new HealthCheckSettings(
                readiness ?? ImmutableList<NamedHealthCheck>.Empty, 
                liveness ?? ImmutableList<NamedHealthCheck>.Empty, 
                "/ready", 
                "/alive", 
                TimeSpan.FromMilliseconds(500));
        
        [Fact]
        public async Task HealthCheckShouldSucceedByDefault()
        {
            var checks = new HealthChecksImpl(_eas, Settings(null, null));
            (await checks.AliveResult()).IsRight.Should().BeTrue();
            (await checks.ReadyResult()).IsRight.Should().BeTrue();
            (await checks.Alive()).Should().BeTrue();
            (await checks.Ready()).Should().BeTrue();
        }
        
        [Fact]
        public async Task HealthCheckShouldSucceedForAllHealthChecksReturningRight()
        {
            var checks = new HealthChecksImpl(
                _eas, 
                Settings(
                    new []{OkCheck}.ToImmutableList(), 
                    new []{OkCheck}.ToImmutableList()));
            (await checks.AliveResult()).IsRight.Should().BeTrue();
            (await checks.ReadyResult()).IsRight.Should().BeTrue();
            (await checks.Alive()).Should().BeTrue();
            (await checks.Ready()).Should().BeTrue();
        }
        
        [Fact]
        public async Task HealthCheckShouldSupportEmptyConstructor()
        {
            var checks = new HealthChecksImpl(
                _eas, 
                Settings(
                    new []{NoargsCtorCheck}.ToImmutableList(), 
                    new []{NoargsCtorCheck}.ToImmutableList()));
            (await checks.AliveResult()).IsRight.Should().BeTrue();
            (await checks.ReadyResult()).IsRight.Should().BeTrue();
            (await checks.Alive()).Should().BeTrue();
            (await checks.Ready()).Should().BeTrue();
        }
        
        [Fact]
        public async Task HealthCheckShouldFailForHealthChecksReturningLeft()
        {
            var checks = new HealthChecksImpl(
                _eas, 
                Settings(
                    new []{FalseCheck}.ToImmutableList(), 
                    new []{FalseCheck}.ToImmutableList()));
            (await checks.AliveResult()).IsRight.Should().BeFalse();
            (await checks.ReadyResult()).IsRight.Should().BeFalse();
            (await checks.Alive()).Should().BeFalse();
            (await checks.Ready()).Should().BeFalse();
        }
        
        [Fact]
        public async Task HealthCheckShouldThrowForAllHealthChecksFail()
        {
            var checks = new HealthChecksImpl(
                _eas, 
                Settings(
                    new []{ThrowsCheck}.ToImmutableList(), 
                    new []{ThrowsCheck}.ToImmutableList()));

            await checks.Invoking(async a => await a.AliveResult()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
            
            await checks.Invoking(async a => await a.ReadyResult()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
            
            await checks.Invoking(async a => await a.Ready()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
            await checks.Invoking(async a => await a.Alive()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
        }
        
        [Fact]
        public async Task HealthCheckShouldThrowIfAnyOfTheChecksFail()
        {
            var checkList = new[] {OkCheck, ThrowsCheck, FalseCheck}.ToImmutableList();
            var checks = new HealthChecksImpl(_eas, Settings(checkList, checkList));

            await checks.Invoking(async a => await a.AliveResult()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
            
            await checks.Invoking(async a => await a.ReadyResult()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
            
            await checks.Invoking(async a => await a.Ready()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
            await checks.Invoking(async a => await a.Alive()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Throws] failed: Exception of type 'Akka.Management.Tests.HealthCheckSpec+TE' was thrown.");
        }
        
        [Fact]
        public async Task HealthCheckShouldThrowIfCheckThrows()
        {
            var checkList = new[] {NaughtyCheck}.ToImmutableList();
            var checks = new HealthChecksImpl(_eas, Settings(checkList, checkList));

            await checks.Invoking(async a => await a.AliveResult()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Naughty] failed: bad");
            
            await checks.Invoking(async a => await a.ReadyResult()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Naughty] failed: bad");
            
            await checks.Invoking(async a => await a.Ready()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Naughty] failed: bad");
            await checks.Invoking(async a => await a.Alive()).Should()
                .ThrowAsync<CheckFailedException>()
                .WithMessage(
                    "Check [Naughty] failed: bad");
        }
        
        [Fact]
        public async Task HealthCheckShouldThrowIfCheckTimesOut()
        {
            var checkList = new[] {SlowCheck, OkCheck}.ToImmutableList();
            var checks = new HealthChecksImpl(_eas, Settings(checkList, checkList));

            await checks.Invoking(async a => await a.AliveResult()).Should()
                .ThrowAsync<CheckTimeoutException>()
                .WithMessage(
                    "Check [Slow] timed out after 500 milliseconds.");
            
            await checks.Invoking(async a => await a.ReadyResult()).Should()
                .ThrowAsync<CheckTimeoutException>()
                .WithMessage(
                    "Check [Slow] timed out after 500 milliseconds.");
            
            await checks.Invoking(async a => await a.Ready()).Should()
                .ThrowAsync<CheckTimeoutException>()
                .WithMessage(
                    "Check [Slow] timed out after 500 milliseconds.");
            await checks.Invoking(async a => await a.Alive()).Should()
                .ThrowAsync<CheckTimeoutException>()
                .WithMessage(
                    "Check [Slow] timed out after 500 milliseconds.");
        }
        
        [Fact]
        public void HealthCheckShouldProvideUsefulErrorIfUserCtorIsInvalid()
        {
            var o = new object();
            o.Invoking(_ =>
                {
                    var checkList = new[] {InvalidCtorCheck}.ToImmutableList();
                    var checks = new HealthChecksImpl(_eas, Settings(checkList, checkList));
                }).Should()
                .Throw<InvalidHealthCheckException>()
                .WithMessage(
                    "Health checks: [Akka.Management.Tests.HealthCheckSpec+InvalidCtor, Akka.Management.Tests] Must have an empty constructor or a constructor that takes an ActorSystem.");
        }

        [Fact]
        public void HealthCheckShouldProvideUsefulErrorIfInvalidType()
        {
            var o = new object();
            o.Invoking(_ =>
                {
                    var checkList = new[] {WrongTypeCheck}.ToImmutableList();
                    var checks = new HealthChecksImpl(_eas, Settings(checkList, checkList));
                }).Should()
                .Throw<InvalidHealthCheckException>()
                .WithMessage(
                    "Health checks: [Akka.Management.Tests.HealthCheckSpec+WrongType, Akka.Management.Tests] Must implement Akka.Management.Dsl.IHealthCheck.");
        }

        [Fact]
        public void HealthCheckShouldProvideUsefulErrorIfClassNotFound()
        {
            var o = new object();
            o.Invoking(_ =>
                {
                    var checkList = new[] {DoesNotExist, OkCheck}.ToImmutableList();
                    var checks = new HealthChecksImpl(_eas, Settings(checkList, checkList));
                }).Should()
                .Throw<InvalidHealthCheckException>()
                .WithMessage(
                    "Health checks: [Akka.Management.Tests.HealthCheckSpec+DoesNotExist, Akka.Management.Tests] Could not load class type.");
        }
        
        [Fact]
        public void HealthCheckShouldProvideUsefulErrorIfCtorThrows()
        {
            var o = new object();
            var inner = o.Invoking(_ =>
                {
                    var checkList = new[] {OkCheck, CtorExceptionCheck}.ToImmutableList();
                    var checks = new HealthChecksImpl(_eas, Settings(checkList, checkList));
                }).Should()
                .Throw<InvalidHealthCheckException>()
                .WithMessage(
                    "Health checks: [Akka.Management.Tests.HealthCheckSpec+CtorException, Akka.Management.Tests] Uncaught exception from Health check construction.");
        }
        
        
        [Fact]
        public async Task HealthCheckShouldBePossibleToDefineViaActorSystemSetup()
        {
            var readinessSetup = new ReadinessCheckSetup(system => new[] {(IHealthCheck)new Ok(system), new False(system)}.ToImmutableList());
            var livenessSetup = new LivenessCheckSetup(system => new[] {(IHealthCheck)new False(system)}.ToImmutableList());
            // bootstrapSetup is needed for config (otherwise default config)
            var bootstrapSetup = BootstrapSetup.Create().WithConfig(ConfigurationFactory.ParseString("some=thing"));
            var actorSystemSetup = ActorSystemSetup.Create(bootstrapSetup, readinessSetup, livenessSetup);

            var sys2 = (ExtendedActorSystem) ActorSystem.Create("HealthCkeckSpec2", actorSystemSetup);

            try
            {
                var checks = new HealthChecksImpl(sys2, Settings(null, null));
                (await checks.AliveResult()).Should().BeOfType<Left<string, Done>>();
                (await checks.ReadyResult()).Should().BeOfType<Left<string, Done>>();
                (await checks.Alive()).Should().BeFalse();
                (await checks.Ready()).Should().BeFalse();
            }
            finally
            {
                Shutdown(sys2);
            }
        }
    }
}
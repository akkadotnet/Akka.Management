//-----------------------------------------------------------------------
// <copyright file="HealthChecksImpl.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Management.Dsl;
using Akka.Util;

namespace Akka.Management.Internal
{
    internal sealed class CheckFailedException : Exception
    {
        public CheckFailedException(string message, Exception innerException) : base(message, innerException)
        { }
    }

    internal sealed class CheckTimeoutException : Exception
    {
        public CheckTimeoutException(string message) : base(message)
        { }
    }

    internal sealed class ClassNotFoundException : Exception
    { }

    internal sealed class HealthChecksImpl : HealthChecks
    {
        private readonly ExtendedActorSystem _system;
        private readonly HealthCheckSettings _settings;
        private readonly ILoggingAdapter _log;

        private readonly ImmutableList<IHealthCheck> _readiness = ImmutableList<IHealthCheck>.Empty;
        private readonly ImmutableList<IHealthCheck> _liveness = ImmutableList<IHealthCheck>.Empty;
        
        public HealthChecksImpl(ExtendedActorSystem system, HealthCheckSettings settings)
        {
            _system = system;
            _settings = settings;
            _log = Logging.GetLogger(system, typeof(HealthChecksImpl));
            
            _log.Info("Loading readiness checks [{0}]",
                string.Join(", ", _settings.ReadinessChecks.Select(a => $"{{Name: {a.Name}, FQCN: {a.Fqcn}}}")));
            _log.Info("Loading liveness checks [{0}]",
                string.Join(", ", _settings.LivenessChecks.Select(a => $"{{Name: {a.Name}, FQCN: {a.Fqcn}}}")));

            var readinessFromSetup = system.Settings.Setup.Get<ReadinessCheckSetup>();
            if (readinessFromSetup.HasValue)
                _readiness = readinessFromSetup.Value.CreateHealthChecks(system);
            _readiness = _readiness.AddRange(Load(settings.ReadinessChecks));

            var livenessFromSetup = system.Settings.Setup.Get<LivenessCheckSetup>();
            if(livenessFromSetup.HasValue)
                _liveness = livenessFromSetup.Value.CreateHealthChecks(system);
            _liveness = _liveness.AddRange(Load(settings.LivenessChecks));
        }

        private ImmutableList<IHealthCheck> Load(ImmutableList<HealthCheckSettings.NamedHealthCheck> checks)
        {
            var result = new List<IHealthCheck>();
            foreach (var check in checks)
            {
                try
                {
                    result.Add(LoadHealthCheck(check.Fqcn));
                }
                catch (Exception ex)
                {
                    throw ex switch
                    {
                        MissingMethodException e => new InvalidHealthCheckException(
                            $"Health checks: [{check.Fqcn}] Must have an empty constructor or a constructor that takes an ActorSystem.", e),
                        InvalidCastException e => new InvalidHealthCheckException(
                            $"Health checks: [{check.Fqcn}] Must implement Akka.Management.Dsl.IHealthCheck.", e),
                        ClassNotFoundException _ => new InvalidHealthCheckException(
                            $"Health checks: [{check.Fqcn}] Could not load class type."),
                        var e => new InvalidHealthCheckException($"Health checks: [{check.Fqcn}] Uncaught exception from Health check construction.", e)
                    };
                }
            }

            return result.ToImmutableList();
        }

        private IHealthCheck LoadHealthCheck(string fqcn)
        {
            var type = Type.GetType(fqcn);
            if (type is null)
                throw new ClassNotFoundException();

            try
            {
                return (IHealthCheck) Activator.CreateInstance(type, _system);
            }
            catch (MissingMethodException)
            {
                return (IHealthCheck) Activator.CreateInstance(type);
            }
        }
        
        public override async Task<bool> Ready()
            => (await ReadyResult()).IsRight;

        public override async Task<Either<string, Done>> ReadyResult()
        {
            try
            {
                if(_log.IsDebugEnabled)
                    _log.Debug("Readiness endpoint called.");
                var result = await Check(_readiness);
                if (result is Left<string, Done> left)
                {
                    _log.Info(left.Value);
                }

                return result;
            }
            catch (Exception e)
            {
                _log.Warning(e, e.Message);
                throw;
            }
        }

        public override async Task<bool> Alive()
            => (await AliveResult()).IsRight;

        public override async Task<Either<string, Done>> AliveResult()
        {
            try
            {
                if(_log.IsDebugEnabled)
                    _log.Debug("Liveliness endpoint called.");
                var result = await Check(_liveness);
                if (result is Left<string, Done> left)
                {
                    _log.Info(left.Value);
                }

                return result;
            }
            catch (Exception e)
            {
                _log.Warning(e, e.Message);
                throw;
            }
        }

        private async Task<Either<string, Done>> Check(ImmutableList<IHealthCheck> checks)
        {
            async Task<Either<string, Done>> ExecuteCheck(IHealthCheck check)
            {
                var cts = new CancellationTokenSource(_settings.CheckTimeout);
                var checkName = check.GetType().Name;
                try
                {
                    var result = await check.Execute(cts.Token);
                    return result
                        ? (Either<string, Done>) new Right<string, Done>(Done.Instance)
                        : new Left<string, Done>($"Check [{checkName}] not ok");
                }
                catch (Exception e)
                {
                    if (e is TaskCanceledException)
                        throw new CheckTimeoutException($"Check [{checkName}] timed out after {_settings.CheckTimeout.TotalMilliseconds} milliseconds.");

                    throw new CheckFailedException($"Check [{checkName}] failed: {e.Message}", e);
                }
            }
            
            var tasks = new List<Task<Either<string, Done>>>();
            foreach (var check in checks)
            {
                tasks.Add(ExecuteCheck(check));
            }

            await Task.WhenAll(tasks);

            var sb = new StringBuilder();
            foreach (var task in tasks)
            {
                switch (task.Result)
                {
                    case Left<string, Done> e:
                        sb.AppendLine(e.ToString());
                        break;
                }
            }

            return sb.Length > 0 
                ? (Either<string, Done>) new Left<string, Done>(sb.ToString()) 
                : new Right<string, Done>(Done.Instance);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.ExceptionServices;
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
                var loaded = TryLoadHealthCheck(check.Fqcn);
                if (!loaded.IsSuccess)
                {
                    switch (loaded.Failure.Value)
                    {
                        case MissingMethodException e:
                            throw new InvalidHealthCheckException(
                                $"Health checks: [{check.Fqcn}] Must have an empty constructor or a constructor that takes an ActorSystem", e);
                        case InvalidCastException e:
                            throw new InvalidHealthCheckException(
                                $"Health checks: [{check.Fqcn}] Must derive from IHealthCheck.", e);
                        case ClassNotFoundException _:
                            throw new InvalidHealthCheckException(
                                $"Health checks: [{check.Fqcn}] Could not load class type.");
                        case var e:
                            throw new InvalidHealthCheckException("Uncaught exception from Health check construction.", e);
                    }
                }
                result.Add(loaded.Get());
            }

            return result.ToImmutableList();
        }

        private Try<IHealthCheck> TryLoadHealthCheck(string fqcn)
        {
            var type = Type.GetType(fqcn);
            if (type is null)
                return new Try<IHealthCheck>(new ClassNotFoundException());
            
            return Try<IHealthCheck>.From(() => (IHealthCheck) Activator.CreateInstance(type, _system))
                .RecoverWith(e =>
                {
                    if (!(e is MissingMethodException))
                    {
                        ExceptionDispatchInfo.Capture(e).Throw();
                        return null;
                    }

                    return new Try<IHealthCheck>((IHealthCheck) Activator.CreateInstance(type));
                });
        }
        
        public override async Task<bool> Ready()
        {
            var result = await ReadyResult();
            return result.IsSuccess && result.Success.Value is Right<string, Done>;
        }

        public override async Task<Try<Either<string, Done>>> ReadyResult()
        {
            try
            {
                var result = await Check(_readiness);
                if (result is Left<string, Done> left)
                {
                    _log.Info(left.Value);
                }

                return new Try<Either<string, Done>>(result);
            }
            catch (Exception e)
            {
                _log.Warning(e, e.Message);
                return new Try<Either<string, Done>>(e);
            }
        }

        public override async Task<bool> Alive()
        {
            var result = await AliveResult();
            return result.IsSuccess && result.Success.Value is Right<string, Done>;
        }

        public override async Task<Try<Either<string, Done>>> AliveResult()
        {
            try
            {
                var result = await Check(_liveness);
                if (result is Left<string, Done> left)
                {
                    _log.Info(left.Value);
                }

                return new Try<Either<string, Done>>(result);
            }
            catch (Exception e)
            {
                _log.Warning(e, e.Message);
                return new Try<Either<string, Done>>(e);
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
                    if (!await check.Execute(cts.Token))
                    {
                        return new Left<string, Done>($"Check {checkName} not OK.");
                    }

                    return new Right<string, Done>(Done.Instance);
                }
                catch (TaskCanceledException)
                {
                    return new Left<string, Done>($"Check [{checkName}] timed out after {_settings.CheckTimeout}");
                }
                catch (Exception e)
                {
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
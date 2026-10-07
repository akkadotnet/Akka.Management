// -----------------------------------------------------------------------
//  <copyright file="AzureLease.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Coordination.Azure.Internal;
using Akka.Event;
using Akka.Util;
using Akka.Util.Internal;

#nullable enable
namespace Akka.Coordination.Azure
{
    public sealed class AzureLease: Lease
    {
        public static Config DefaultConfiguration
            => ConfigurationFactory.FromResource<AzureLease>("Akka.Coordination.Azure.reference.conf");

        public const string ConfigPath = "akka.coordination.lease.azure";
        private static readonly AtomicCounter LeaseCounter = new AtomicCounter(1);

        
        private static string TruncateTo63Characters(string name) => name.Length > 63 ? name.Substring(0, 63) : name;

        private static readonly Regex Rx1 = new Regex("[_.]");
        private static readonly Regex Rx2 = new Regex("[^-a-z0-9]");
        private static string MakeDns1039Compatible(string name)
        {
            var normalized = name.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
            normalized = Rx1.Replace(normalized, "-");
            normalized = Rx2.Replace(normalized, "");
            return TruncateTo63Characters(normalized).Trim('_');
        }

        private readonly ILoggingAdapter _log;
        private readonly AtomicBoolean _leaseTaken;
        private readonly LeaseSettings _settings;
        private readonly TimeSpan _timeout;
        private readonly string _leaseName;
        private readonly IActorRef _leaseActor;
        private readonly object _acquireLock = new ();
        private Task<bool>? _acquireTask;

        public AzureLease(LeaseSettings settings, ExtendedActorSystem system) :
            this(system, new AtomicBoolean(), settings)
        { }

        // ReSharper disable once MemberCanBePrivate.Global
        public AzureLease(ExtendedActorSystem system, AtomicBoolean leaseTaken, LeaseSettings settings): base(settings)
        {
            _leaseTaken = leaseTaken;
            _settings = settings;
            
            _log = Logging.GetLogger(system, GetType());
            var azureLeaseSettings = AzureLeaseSettings.Create(system, settings.TimeoutSettings);

            var setup = system.Settings.Setup.Get<AzureLeaseSetup>();
            if (setup.HasValue)
                azureLeaseSettings = setup.Value.Apply(azureLeaseSettings, system);
            
            _timeout = _settings.TimeoutSettings.OperationTimeout;
            _leaseName = MakeDns1039Compatible(settings.LeaseName);
            
            if(!_leaseName.Equals(settings.LeaseName))
                _log.Info("Original lease name [{0}] sanitized for Azure blob name: [{1}]", settings.LeaseName, _leaseName);

            var client = new AzureApiImpl(system, azureLeaseSettings);
            
            _leaseActor = system.SystemActorOf(
                LeaseActor.Props(client, settings, _leaseName, leaseTaken)
                    .WithDeploy(Deploy.Local),
                $"AzureLease{LeaseCounter.GetAndIncrement()}");
            
            _log.Debug(
                "Starting Azure lease actor [{0}] for lease [{1}], owner [{2}]",
                _leaseActor,
                _leaseName,
                settings.OwnerName);
        }

        public override bool CheckLease()
            => _leaseTaken.Value;
        
        public override async Task<bool> Release()
        {
            try
            {
                if(_log.IsDebugEnabled)
                    _log.Debug("Releasing lease [{0}] for [{1}]", _leaseName, _settings.OwnerName);
                var result = await _leaseActor.Ask(LeaseActor.Release.Instance, _timeout);
                switch (result)
                {
                        case LeaseActor.LeaseReleased:
                            return true;
                        case LeaseActor.InvalidReleaseRequest:
                            _log.Info("Tried to release a lease [{0}] for [{1}] that is not acquired", _leaseName, _settings.OwnerName);
                            return true;
                        case Status.Failure f:
                            throw new LeaseException($"Failure while releasing lease [{_leaseName}] for [{_settings.OwnerName}]: {f.Cause.Message}", f.Cause);
                        default:
                            throw new LeaseException($"Unexpected response type wile releasing lease [{_leaseName}] for [{_settings.OwnerName}]: [{result.GetType()}] {result}");
                }
            }
            catch (AskTimeoutException)
            {
                throw new LeaseTimeoutException(
                    $"Timed out trying to release lease [{_leaseName}] for [{_settings.OwnerName}]. It may still be taken.");
            }
        }

        // Akka.NET 1.6 annotates the lease-lost callback as Action<Exception>, but a lease lost to a
        // heartbeat conflict has no exception to report, so the LeaseActor passes null, as it did on 1.5.
        // Akka's own lease consumers (sharding, singleton) already handle a null reason.
        private static Action<Exception?>? ToInternalCallback(Action<Exception>? leaseLostCallback)
            => leaseLostCallback is null ? null : reason => leaseLostCallback(reason!);

        public override Task<bool> Acquire()
            => Acquire(null);

        public override Task<bool> Acquire(Action<Exception>? leaseLostCallback)
        {
            lock (_acquireLock)
            {
                if (_acquireTask is not null)
                {
                    if(_log.IsDebugEnabled)
                        _log.Debug("Lease [{0}] for [{1}] is already being acquired", _leaseName, _settings.OwnerName);
                    return _acquireTask;
                }

                if(_log.IsDebugEnabled)
                    _log.Debug("Acquiring lease [{0}] for [{1}]", _leaseName, _settings.OwnerName);
                _acquireTask = AcquireTask();
                
                return _acquireTask;
            }
            
            async Task<bool> AcquireTask()
            {
                try
                {
                    using var cts = new CancellationTokenSource(_timeout);
                    var result = await _leaseActor.Ask(new LeaseActor.Acquire(ToInternalCallback(leaseLostCallback)), cts.Token);
                    cts.Token.ThrowIfCancellationRequested();

                    return result switch
                    {
                        LeaseActor.LeaseAcquired => true,
                        LeaseActor.LeaseTaken => false,
                        Status.Failure f => throw new LeaseException($"Failure while acquiring lease [{_leaseName}] for [{_settings.OwnerName}]: {f.Cause.Message}", f.Cause),
                        _ => throw new LeaseException($"Unexpected response type while acquiring lease [{_leaseName}] for [{_settings.OwnerName}]: [{result.GetType()}] {result}")
                    };
                }
                catch (AskTimeoutException ex)
                {
                    throw new LeaseTimeoutException($"Timed out trying to acquire lease [{_leaseName}] for [{_settings.OwnerName}]. It may still be taken.", ex);
                }
                catch (AggregateException ex)
                {
                    var flattened = ex.Flatten();
                    if (flattened.InnerExceptions.Any(e => e is AskTimeoutException))
                        throw new LeaseTimeoutException($"Timed out trying to acquire lease [{_leaseName}] for [{_settings.OwnerName}]. It may still be taken.", ex);

                    throw new LeaseException($"Faulted trying to acquire lease [{_leaseName}] for [{_settings.OwnerName}]. It may still be taken.", ex);
                }
                catch (Exception ex)
                {
                    throw new LeaseException($"Faulted trying to acquire lease [{_leaseName}] for [{_settings.OwnerName}]. It may still be taken.", ex);
                }
                finally
                {
                    _acquireTask = null;
                }
            }            
        }
        
    }
}
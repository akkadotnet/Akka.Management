﻿//-----------------------------------------------------------------------
// <copyright file="KubernetesLease.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Coordination.KubernetesApi.Internal;
using Akka.Event;
using Akka.Util;
using Akka.Util.Internal;

#nullable enable
namespace Akka.Coordination.KubernetesApi
{
    public class KubernetesLease : Lease
    {
        public static Config DefaultConfiguration
            => ConfigurationFactory.FromResource<KubernetesLease>("Akka.Coordination.KubernetesApi.reference.conf");

        public const string ConfigPath = "akka.coordination.lease.kubernetes";
        private static readonly AtomicCounter LeaseCounter = new AtomicCounter(1);

        private static string TruncateTo63Characters(string name) => name.Length > 63 ? name.Substring(0, 63) : name;

        private static readonly Regex Rx1 = new ("[_.]", RegexOptions.Compiled);
        private static readonly Regex Rx2 = new ("[^-a-z0-9]", RegexOptions.Compiled);
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

        public KubernetesLease(LeaseSettings settings, ExtendedActorSystem system) :
            this(system, new AtomicBoolean(), settings)
        { }

        // ReSharper disable once MemberCanBePrivate.Global
        public KubernetesLease(ExtendedActorSystem system, AtomicBoolean leaseTaken, LeaseSettings settings): base(settings)
        {
            _leaseTaken = leaseTaken;
            _settings = settings;
            
            _log = Logging.GetLogger(system, GetType());
            var kubernetesSettings = KubernetesSettings.Create(settings);

            var setup = system.Settings.Setup.Get<KubernetesLeaseSetup>();
            if (setup.HasValue)
                kubernetesSettings = setup.Value.Apply(kubernetesSettings);
            
            var client = new KubernetesApiImpl(system, kubernetesSettings);
            _timeout = _settings.TimeoutSettings.OperationTimeout;
            _leaseName = MakeDns1039Compatible(settings.LeaseName);
            _leaseActor = system.ActorOf(
                LeaseActor.Props(client, settings, _leaseName, leaseTaken),
                $"KubernetesLease{LeaseCounter.GetAndIncrement()}");
            
            if(!_leaseName.Equals(settings.LeaseName))
                _log.Info("Original lease name [{0}] sanitized for kubernetes: [{1}]", settings.LeaseName, _leaseName);

            _log.Debug(
                "Starting kubernetes lease actor [{0}] for lease [{1}], owner [{2}]",
                _leaseActor,
                _leaseName,
                settings.OwnerName);
        }

        public override bool CheckLease()
            => _leaseTaken.Value;
        
        public override Task<bool> Release()
        {
            // replace with transform once 2.11 dropped
            try
            {
                if(_log.IsDebugEnabled)
                    _log.Debug("Releasing lease");
                return _leaseActor.Ask(LeaseActor.Release.Instance, _timeout)
                    .ContinueWith(t =>
                    {
                        return t.Result switch
                        {
                            LeaseActor.LeaseReleased _ => true,
                            LeaseActor.InvalidRequest req => throw new LeaseException(req.Reason),
                            _ => false
                        };
                    });
            }
            catch (AskTimeoutException)
            {
                throw new LeaseTimeoutException(
                    $"Timed out trying to release lease [{_leaseName}, {_settings.OwnerName}]. It may still be taken.");
            }
        }

        public override Task<bool> Acquire()
            => Acquire(null);

        public override Task<bool> Acquire(Action<Exception?>? leaseLostCallback)
        {
            // replace with transform once 2.11 dropped
            try
            {
                if(_log.IsDebugEnabled)
                    _log.Debug("Acquiring lease");
                return _leaseActor.Ask(new LeaseActor.Acquire(leaseLostCallback), _timeout)
                    .ContinueWith(t =>
                    {
                        return t.Result switch
                        {
                            LeaseActor.LeaseAcquired _ => true,
                            LeaseActor.LeaseTaken _ => false,
                            LeaseActor.InvalidRequest req => throw new LeaseException(req.Reason),
                            _ => false
                        };
                    });
            }
            catch (AskTimeoutException)
            {
                throw new LeaseTimeoutException(
                    $"Timed out trying to acquire lease [{_leaseName}, {_settings.OwnerName}]. It may still be taken.");
            }
        }
    }
}
using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Util;
using Akka.Util.Internal;

namespace Akka.Coordination.Azure
{
    /// <summary>
    /// INTERNAL API
    /// </summary>
    internal static class AzureLeaseHelpers
    {
        public const string ConfigPath = "akka.coordination.lease.azure";

        public static readonly AtomicCounter LeaseCounter = new AtomicCounter(0);

        public static Config DefaultConfig { get; }

        static AzureLeaseHelpers()
        {
            DefaultConfig = ConfigurationFactory
                .FromResource<AzureLeaseConfig>("Akka.Coordination.Azure.reference.conf");
        }
    }

    public class AzureLease : Lease
    {
        public AzureLease(LeaseSettings settings, ExtendedActorSystem actorSystem, AtomicBoolean leaseTaken) 
            : base(settings)
        {
            _system = actorSystem;
            _leaseTaken = leaseTaken;
            _log = Logging.GetLogger(_system, typeof(AzureLease));

            // per https://docs.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata
            _leaseName = Uri.EscapeUriString(Settings.LeaseName);
            if (_leaseName != Settings.LeaseName)
            {
                _log.Info("Original lease name [{0}] sanitized for Azure Blob Storage: [{1}]",
                    Settings.LeaseName, _leaseName);
            }
        }

        public AzureLease(LeaseSettings settings, ExtendedActorSystem actorSystem) 
            : this(settings, actorSystem, new AtomicBoolean(false))
        {
        }

        private readonly ExtendedActorSystem _system;
        private readonly AtomicBoolean _leaseTaken;
        private readonly ILoggingAdapter _log;
        private string _leaseName;


        public override async Task<bool> Acquire()
        {
            throw new NotImplementedException();
        }

        public override async Task<bool> Acquire(Action<Exception> leaseLostCallback)
        {
            throw new NotImplementedException();
        }

        public override async Task<bool> Release()
        {
            throw new NotImplementedException();
        }

        public override bool CheckLease()
        {
            throw new NotImplementedException();
        }
    }
}

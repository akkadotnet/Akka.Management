using System;
using Akka.Annotations;

namespace Akka.Management
{
    /// <summary>
    /// Settings object used to pass through information about the environment the routes will be running in,
    /// from the component starting the actual HTTP server, to the <see cref="IManagementRouteProvider"/>.
    /// 
    /// Not for user extension.
    /// </summary>
    [DoNotInherit]
    public abstract class ManagementRouteProviderSettings
    {
        /// <summary>
        /// The "self" base Uri which points to the root of the HTTP server running the route provided by the Provider.
        /// Can be used to introduce some self-awareness and/or links to "self" in the routes created by the Provider.
        /// </summary>
        public Uri SelfBaseUri { get; }

        public bool ReadOnly { get; }

        public static ManagementRouteProviderSettings Create(Uri selfBaseUri, bool readOnly) =>
            new ManagementRouteProviderSettingsImpl(selfBaseUri, readOnly);

        protected ManagementRouteProviderSettings(Uri selfBaseUri, bool readOnly)
        {
            SelfBaseUri = selfBaseUri;
            ReadOnly = readOnly;
        }

        /// <summary>
        /// Should only readOnly routes be provided. It is up to each provider to define what readOnly means.
        /// </summary>
        public abstract ManagementRouteProviderSettings WithReadOnly(bool readOnly);
    }

    [InternalApi]
    public sealed class ManagementRouteProviderSettingsImpl : ManagementRouteProviderSettings
    {
        public ManagementRouteProviderSettingsImpl(Uri selfBaseUri, bool readOnly)
            : base(selfBaseUri, readOnly)
        { }

        public override ManagementRouteProviderSettings WithReadOnly(bool readOnly) => Copy(readOnly: readOnly);

        private ManagementRouteProviderSettings Copy(Uri selfBaseUri = null, bool? readOnly = null) =>
            new ManagementRouteProviderSettingsImpl(
                selfBaseUri: selfBaseUri ?? SelfBaseUri,
                readOnly: readOnly ?? ReadOnly);
    }
}
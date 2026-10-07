//-----------------------------------------------------------------------
// <copyright file="ExtensionAutoStart.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Linq;
using Akka.Actor;
using Akka.Actor.Setup;

namespace Akka.Management
{
    internal static class ExtensionAutoStart
    {
        /// <summary>
        /// True when the <see cref="ActorSystem"/> was told to load <typeparamref name="TProvider"/> at startup:
        /// either by type name in the <c>akka.extensions</c> HOCON list, or as an extension id in an
        /// <see cref="ExtensionsSetup"/>, which is how Akka.Hosting 1.6's <c>WithExtension</c> passes it.
        /// </summary>
        public static bool IsRequested<TProvider>(ExtendedActorSystem system) where TProvider : IExtensionId
        {
            if (system.Settings.Config.GetStringList("akka.extensions")
                .Any(s => s.Contains(typeof(TProvider).Name)))
                return true;

            var setup = system.Settings.Setup.Get<ExtensionsSetup>();
            return setup.HasValue && setup.Value.ExtensionIds.Any(id => id is TProvider);
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="ManagementRouteProvider.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Http.Dsl;

namespace Akka.Management
{
    /// <summary>
    /// Extend this abstract class in your extension in order to allow it to contribute routes to Akka Management starts its HTTP endpoint
    /// </summary>
    public interface IManagementRouteProvider
    {
        /// <summary>
        /// Routes to be exposed by Akka cluster management
        /// </summary>
        Route[] Routes(ManagementRouteProviderSettings settings);
    }
}

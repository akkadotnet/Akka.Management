//-----------------------------------------------------------------------
// <copyright file="IHealthCheck.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Akka.Management.Dsl
{
    public interface IHealthCheck
    {
        Task<bool> Execute(CancellationToken token);
    }
}
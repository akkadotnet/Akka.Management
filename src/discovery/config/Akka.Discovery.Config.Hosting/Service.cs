// -----------------------------------------------------------------------
//  <copyright file="Service.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Text;
using Akka.Configuration;
using Akka.Hosting;

namespace Akka.Discovery.Config.Hosting;

public class Service
{
    public string? Name { get; set; }
    public string[]? Endpoints { get; set; }

    internal StringBuilder Apply(StringBuilder builder)
    {
        if (Name is null)
            throw new ConfigurationException("Service name must not be null");
        if (Endpoints is null)
            throw new ConfigurationException("Service endpoints must not be null");
        if (Endpoints.Length == 0)
            throw new ConfigurationException("There must be at least one endpoint defined");

        builder.AppendLine($"{Name} {{");
        builder.AppendLine($"endpoints = [ { string.Join(",", Endpoints.Select(s => s.ToHocon()))} ]");
        builder.AppendLine("}");

        return builder;
    }
}
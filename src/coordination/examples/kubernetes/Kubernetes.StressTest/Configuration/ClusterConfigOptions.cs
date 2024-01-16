// -----------------------------------------------------------------------
//   <copyright file="ClusterConfigOptions.cs" company="Petabridge, LLC">
//     Copyright (C) 2015-2024 .NET Petabridge, LLC
//   </copyright>
// -----------------------------------------------------------------------

namespace Kubernetes.StressTest.Configuration;

public sealed class ClusterConfigOptions
{
    public string? Ip { get; set; }
    public int? Port { get; set; }
    public string[]? Seeds { get; set; }
}
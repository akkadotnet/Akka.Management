﻿// -----------------------------------------------------------------------
//  <copyright file="ParseState.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Docker.Cluster.Splitter;

public enum ParseState
{
    Num,
    From,
}
// -----------------------------------------------------------------------
//  <copyright file="Exceptions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Akka.Actor;

namespace Akka.Discovery.Azure;

/// <summary>
/// Thrown when a prune operation failed
/// </summary>
public sealed class PruneOperationException: AkkaException
{
    public PruneOperationException(List<string> reasons)
    {
        Reasons = reasons;
    }

    public PruneOperationException(string message, List<string> reasons, Exception? cause = null) : base(message, cause)
    {
        Reasons = reasons;
    }

    public PruneOperationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        Reasons = new List<string>();
    }
    
    public List<string> Reasons { get; }

    public override string Message => $"{base.Message}. Reasons:\n\t-{string.Join("\n\t-", Reasons)}";
}

/// <summary>
/// Thrown when <see cref="ClusterMemberTableClient"/> failed to connect to Azure Table
/// </summary>
public sealed class InitializationException : AkkaException
{
    public InitializationException()
    {
    }

    public InitializationException(string message, Exception? cause = null) : base(message, cause)
    {
    }

    public InitializationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

/// <summary>
/// Thrown when the client failed to update the node entity
/// </summary>
public sealed class UpdateOperationException : AkkaException
{
    public UpdateOperationException()
    {
    }

    public UpdateOperationException(string message, Exception? cause = null) : base(message, cause)
    {
    }

    public UpdateOperationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

/// <summary>
/// Thrown when the client failed to create an entity entry
/// </summary>
public sealed class CreateEntityFailedException : AkkaException
{
    public CreateEntityFailedException()
    {
    }

    public CreateEntityFailedException(string message, Exception cause = null) : base(message, cause)
    {
    }

    public CreateEntityFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
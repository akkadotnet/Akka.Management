//-----------------------------------------------------------------------
// <copyright file="Rejection.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Net.Http;
using System.Net.Mime;
using Akka.Http.Dsl.Model;

namespace Akka.Http.Dsl.Server
{
    public interface IRejection { }

    public interface IRejectionWithOptionalCause : IRejection
    {
        Exception Cause { get; }
    }
    
    public sealed class MethodRejection : IRejection
    {
        public MethodRejection(HttpMethod supported)
        {
            Supported = supported;
        }

        public HttpMethod Supported { get; }
    }

    public sealed class SchemeRejection : IRejection
    {
        public SchemeRejection(string supported)
        {
            Supported = supported;
        }

        public string Supported { get; }
    }

    public sealed class MissingQueryParamRejection : IRejection
    {
        public MissingQueryParamRejection(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }

    public sealed class InvalidRequiredValueForQueryParamRejection : IRejection
    {
        public InvalidRequiredValueForQueryParamRejection(string parameterName, string expectedValue, string actualValue)
        {
            ParameterName = parameterName;
            ExpectedValue = expectedValue;
            ActualValue = actualValue;
        }

        public string ParameterName { get; }
        public string ExpectedValue { get; }
        public string ActualValue { get; }
    }

    public sealed class MalformedQueryParamRejection : IRejectionWithOptionalCause
    {
        public MalformedQueryParamRejection(string parameterName, string errorMsg, Exception cause = null)
        {
            ParameterName = parameterName;
            ErrorMsg = errorMsg;
            Cause = cause;
        }

        public string ParameterName { get; }
        public string ErrorMsg { get; }
        public Exception Cause { get; }
    }

    public sealed class MissingFormFieldRejection : IRejection
    {
        public MissingFormFieldRejection(string fieldName)
        {
            FieldName = fieldName;
        }

        public string FieldName { get; }
    }

    public sealed class MissingHeaderRejection : IRejection
    {
        public MissingHeaderRejection(string headerName)
        {
            HeaderName = headerName;
        }

        public string HeaderName { get; }
    }

    public sealed class MissingAttributeRejection<T> : IRejection
    {
        public MissingAttributeRejection(AttributeKey<T> key)
        {
            Key = key;
        }

        public AttributeKey<T> Key { get; }
    }

    public sealed class MalformedHeaderRejection : IRejectionWithOptionalCause
    {
        public MalformedHeaderRejection(string headerName, string errorMsg, Exception cause = null)
        {
            HeaderName = headerName;
            ErrorMsg = errorMsg;
            Cause = cause;
        }

        public string HeaderName { get; }
        public string ErrorMsg { get; }
        public Exception Cause { get; }
    }

    public sealed class InvalidOriginRejection : IRejection
    {
        public InvalidOriginRejection(ImmutableList<Uri> allowedOrigins)
        {
            AllowedOrigins = allowedOrigins;
        }

        public ImmutableList<Uri> AllowedOrigins { get; } 
    }

    [Serializable]
    public sealed class UnsupportedRequestContentTypeRejection : IRejection
    {
        public UnsupportedRequestContentTypeRejection(
            ImmutableHashSet<ContentType> supported,
            ContentType contentType = null)
        {
            Supported = supported;
            ContentType = contentType;
        }

        public ImmutableHashSet<ContentType> Supported { get; }
        public ContentType ContentType { get; }
    }
}
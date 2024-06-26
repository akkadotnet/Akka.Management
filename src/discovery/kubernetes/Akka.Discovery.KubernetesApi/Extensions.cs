﻿//-----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Linq;
using Akka.Configuration.Hocon;
using Akka.Util.Internal;

namespace Akka.Discovery.KubernetesApi
{
    internal static class ConfigExtensions
    {
        public static string? GetStringIfDefined(this Configuration.Config config, string key)
        {
            var value = config.GetString(key);
            return string.IsNullOrWhiteSpace(value) || value.Equals($"<{key}>") ? null : value;
        }
        
        internal static Configuration.Config MoveTo(this Configuration.Config config, string path)
        {
            var rootObj = new HoconObject();
            var rootValue = new HoconValue();
            rootValue.Values.Add(rootObj);

            var lastObject = rootObj;

            var keys = path.SplitDottedPathHonouringQuotes().ToArray();
            for (var i = 0; i < keys.Length - 1; i++)
            {
                var key = keys[i];
                var innerObject = new HoconObject();
                var innerValue = new HoconValue();
                innerValue.Values.Add(innerObject);

                lastObject.GetOrCreateKey(key);
                lastObject.Items[key] = innerValue;
                lastObject = innerObject;
            }
            lastObject.Items[keys[keys.Length - 1]] = config.Root;

            return new Configuration.Config(new HoconRoot(rootValue));
        }
    }

    internal static class ObjectExtensions
    {
        public static T GetOrElse<T>(this T obj, T @default)
            => obj is null ? @default : obj;

        public static string? DefaultIfNullOrWhitespace(this string? str, string? @default)
            => string.IsNullOrWhiteSpace(str) ? @default : str;
    }
}
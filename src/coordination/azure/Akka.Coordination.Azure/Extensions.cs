//-----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

#nullable enable
namespace Akka.Coordination.Azure
{
    internal static class ConfigExtensions
    {
        public static string GetStringIfDefined(this Configuration.Config config, string key)
        {
            var value = config.GetString(key);
            return string.IsNullOrWhiteSpace(value) || value.Equals($"<{key}>") ? "" : value;
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
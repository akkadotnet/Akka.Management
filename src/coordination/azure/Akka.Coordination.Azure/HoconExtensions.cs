// -----------------------------------------------------------------------
//  <copyright file="HoconExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Akka.Coordination.Azure;

// TODO: Remove this extension class when Akka.Hosting version greater than 1.0.0 has been released 
public static class HoconExtensions
{
    private static readonly Regex EscapeRegex = new ("[][$\"\\\\{}:=,#`^?!@*&]{1}", RegexOptions.Compiled);
        
    public static string ToHocon(this string? text)
    {
        // nullable literal value support
        if (text is null)
            return "null";

        // triple double quote multi line support
        if (text.Contains("\n") && !text.StartsWith("\"\"\"") && !text.EndsWith("\"\"\""))
            return $"\"\"\"{text}\"\"\"";

        // Not going to bother to check quote validity
        if (text.Length > 1 && text.StartsWith("\"") && text.EndsWith("\""))
            return text;
            
        // double quote support
        return text == string.Empty || EscapeRegex.IsMatch(text) ? $"\"{text}\"" : text;
    }
}
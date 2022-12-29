// -----------------------------------------------------------------------
//  <copyright file="AkkaRouter.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Ceen;
using Ceen.Httpd;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Http.Dsl;

public class AkkaRouter : IRouter
{
    private readonly ActorSystem _system;
    private readonly ILoggingAdapter _log;

    public AkkaRouter(ActorSystem system, params Route[] routes): this(system, routes.AsEnumerable())
    {
    }
        
    public AkkaRouter(ActorSystem system) : this(system, Array.Empty<Route>())
    {
    }

    public AkkaRouter(ActorSystem system, IEnumerable<Route> rules)
    {
        _system = system;
        _log = Logging.GetLogger(system, typeof(AkkaRouter));
        Rules = rules.Select(((string key, HttpModuleBase module) x) => (ToRegex(x.key), x.module)).ToList();;
    }

    private static readonly Regex WildcardMatcher = new ("\\*|\\?|[^\\*\\?]+");

    public IList<(Regex?, HttpModuleBase)> Rules { get; set; }

    private static Regex? ToRegex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal) 
            ? new Regex(value.Substring(1, value.Length - 2)) : WildcardExpandToRegex(value);
    }

    private static Regex WildcardExpandToRegex(string value) => new (WildcardMatcher.Replace(value, match =>
    {
        if (match.Value == "*")
            return ".*";
        return match.Value == "?" ? "." : Regex.Escape(match.Value);
    }));
        
    public void Add(string route, HttpModuleBase handler) => Rules.Add((Router.ToRegex(route), handler));

    public void Add(Regex route, HttpModuleBase handler) => Rules.Add((route, handler));

    public async Task<bool> Process(IHttpContext context)
    {
        var akkaContext = new AkkaHttpContext(_system, context);
        foreach (var rule in Rules)
        {
            var (key, module) = rule;
            if (key != null)
            {
                var match = key.Match(context.Request.Path);
                if (!match.Success || match.Length != context.Request.Path.Length)
                    continue;
            }
        
            context.Request.RequireHandler(module.GetType().GetCustomAttributes(typeof (RequireHandlerAttribute), true).OfType<RequireHandlerAttribute>());
            if (await module.HandleAsync(akkaContext))
            {
                _log.Debug($"Request to path {context.Request.Path} completed successfully.");
                return true;
            }
            context.Request.PushHandlerOnStack(module);
        }
        _log.Info($"Request to path {context.Request.Path} rejected: [{HttpStatusCode.NotFound}]");
        return false;
    }
}
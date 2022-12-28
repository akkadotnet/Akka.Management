//-----------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Akka.Http.Extensions
{
    public static class TaskExtensions
    {
        public static Task<TResult> Map<TResult>(this Task source, Func<Task, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return source.ContinueWith(selector, TaskContinuationOptions.NotOnCanceled);
        }        

        public static Task<TResult> Map<TSource, TResult>(this Task<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return source.ContinueWith(t => selector(t.Result), TaskContinuationOptions.NotOnCanceled);
        }

        public static Task WhenComplete<TSource>(this Task<TSource> source, Action<TSource, Exception> continuationAction) =>
            source.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var exception = t.Exception?.InnerExceptions != null && t.Exception.InnerExceptions.Count == 1
                        ? t.Exception.InnerExceptions[0]
                        : t.Exception;

                    continuationAction(default, exception);
                }
                else
                {
                    continuationAction(t.Result, null);
                }
            }, TaskContinuationOptions.NotOnCanceled);
    }
}
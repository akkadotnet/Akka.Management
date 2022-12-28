//-----------------------------------------------------------------------
// <copyright file="ServerBinding.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Http.Extensions;

namespace Akka.Http.Dsl
{
    /// <summary>
    /// Represents a prospective HTTP server binding.
    /// </summary>
    public class ServerBinding
    {
        private readonly Func<TimeSpan, Task<HttpTerminated>> _terminateAction;
        private readonly TaskCompletionSource<TimeSpan> _whenTerminationSignalIssued = new TaskCompletionSource<TimeSpan>();
        private readonly TaskCompletionSource<HttpTerminated> _whenTerminated = new TaskCompletionSource<HttpTerminated>();
        
        // no support for unbind, not a concept in aspnet core
        private static Func<Task> UnbindAction => () => Task.CompletedTask;

        /// <summary>
        /// The local address of the endpoint bound by the materialization of the `connections`
        /// </summary>
        public EndPoint LocalAddress { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerBinding"/> class.
        /// </summary>
        public ServerBinding(EndPoint localAddress, Func<TimeSpan, Task<HttpTerminated>> terminateAction)
        {
            LocalAddress = localAddress;
            _terminateAction = terminateAction;
        }

        /// <summary>
        /// Completes when <see cref="Terminate"/> is called and server termination is in progress.
        /// Can be useful to make parts of your application aware that termination has been issued,
        /// and they have <see cref="TimeSpan"/> time remaining to clean-up before the server will forcefully close
        /// existing connections.
        /// </summary>
        public Task<TimeSpan> WhenTerminationSignalIssued => _whenTerminationSignalIssued.Task;

        /// <summary>
        /// This <see cref="Task"/> completes when the termination process, as initiated by an <see cref="Terminate"/> call has completed.
        /// </summary>
        public Task<HttpTerminated> WhenTerminated => _whenTerminated.Task;

        /// <summary>
        /// Asynchronously triggers the unbinding of the port that was bound by the materialization of the `connections`.
        /// The produced <see cref="Task"/>> is fulfilled when the unbinding has been completed.
        /// </summary>
        public Task<Done> Unbind() => UnbindAction().Map(_ => Done.Instance);

        /// <summary>
        /// Triggers "graceful" termination request being handled on this connection.
        /// </summary>
        /// <param name="hardDeadline">timeout after which all requests and connections shall be forcefully terminated</param>
        public async Task<HttpTerminated> Terminate(TimeSpan hardDeadline)
        {
            _whenTerminationSignalIssued.TrySetResult(hardDeadline);
            await UnbindAction();
            var terminate = await _terminateAction(hardDeadline);
            _whenTerminated.TrySetResult(terminate);
            return WhenTerminated.Result;
        }

        /// <summary>
        /// Adds this <see cref="ServerBinding"/> to the actor system's coordinated shutdown, so that <see cref="Unbind"/>
        /// and <see cref="Terminate"/> get called appropriately before the system is shut down.
        /// </summary>
        /// <param name="hardTerminationDeadline">timeout after which all requests and connections shall be forcefully terminated</param>
        /// <param name="system">TBD</param>
        public ServerBinding AddToCoordinatedShutdown(TimeSpan hardTerminationDeadline, ActorSystem system)
        {
            var shutdown = CoordinatedShutdown.Get(system);
            shutdown.AddTask(CoordinatedShutdown.PhaseServiceUnbind, $"http-unbind-{LocalAddress}", Unbind);
            shutdown.AddTask(CoordinatedShutdown.PhaseServiceRequestsDone, $"http-terminate-{LocalAddress}", async () =>
            {
                await Terminate(hardTerminationDeadline);
                return Done.Instance;
            });            
            return this;
        }
    }
}
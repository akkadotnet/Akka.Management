using System;
using System.Threading.Tasks;
using Akka.Http.Dsl.Model;
using Akka.IO;
using Microsoft.AspNetCore.Http;

namespace Akka.Http.Internal
{
    /// <summary>
    /// Middleware for transforming AspNetCore requests into Akka-Http requests.
    /// </summary>
    public class HttpRequestMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRequestMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the processing pipeline.</param>
        public HttpRequestMiddleware(RequestDelegate next) => _next = next;

        /// <summary>
        /// The main invocation of the middleware component.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> in the pipeline.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var input = new byte[Convert.ToInt32(context.Request.ContentLength)];
            await context.Request.Body.ReadAsync(input, 0, input.Length);
            
            var request = Dsl.Model.HttpRequest.Create(
                context.Request.Method, 
                context.Request.Path.Value, 
                new RequestEntity(context.Request.ContentType, ByteString.FromBytes(input)));
            
            // set the feature
            context.Features.Set(request);
            await _next(context);
        }
    }
}
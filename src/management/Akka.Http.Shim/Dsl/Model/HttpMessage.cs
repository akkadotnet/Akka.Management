using System.Collections.Immutable;
using Akka.Annotations;
using Akka.IO;

namespace Akka.Http.Dsl.Model
{
    /// <summary>
    /// The base type for an Http message (request or response).
    /// </summary>
    [DoNotInherit]
    public abstract class HttpMessage<T>
    {
        /// <summary>
        /// The entity of this message.
        /// </summary>
        public abstract ResponseEntity Entity { get; }

        /// <summary>
        /// Returns a copy of this message with the entity set to the given one.
        /// </summary>
        public abstract T WithEntity(RequestEntity entity);

        public T WithEntity(string content) =>
            WithEntity((RequestEntity)HttpEntity.Create(content));

        public T WithEntity(ByteString bytes) =>
            WithEntity((RequestEntity)HttpEntity.Create(bytes));

        public T WithEntity(string contentType, string content) =>
            WithEntity((RequestEntity)HttpEntity.Create(contentType, content));
    }

    /// <summary>
    /// The immutable HTTP request model.
    /// </summary>
    public sealed class HttpRequest : HttpMessage<HttpRequest>
    {
        /// <summary>
        /// Returns the Http method of this request.
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// Returns the Uri of this request.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Returns the entity of this request.
        /// </summary>
        public override ResponseEntity Entity { get; }

        /// <summary>
        /// Returns a default request to be modified using the `WithX` methods.
        /// </summary>
        public static HttpRequest Create(string method, string path, ResponseEntity entity = null) =>
            new HttpRequest(method, path, entity);

        private HttpRequest(string method, string path, ResponseEntity entity)
        {
            Method = method;
            Path = path;
            Entity = entity;
        }

        /// <inheritdoc />
        public override HttpRequest WithEntity(RequestEntity entity) => Copy(entity: entity);

        /// <summary>
        /// Returns a copy of this instance with a new method.
        /// </summary>
        public HttpRequest WithMethod(string method) => Copy(method: method);

        /// <summary>
        /// Returns a copy of this instance with a new Uri.
        /// </summary>
        public HttpRequest WithPath(string path) => Copy(path: path);

        private HttpRequest Copy(string method = null, string path = null,RequestEntity entity = null) =>
            new HttpRequest(method ?? Method, path ?? Path, entity ?? Entity);
    }

    /// <summary>
    /// The immutable HTTP response model.
    /// </summary>
    public sealed class HttpResponse : HttpMessage<HttpResponse>
    {
        /// <summary>
        /// Returns the status-code of this response.
        /// </summary>
        public int Status { get; }

        /// <summary>
        /// An enumerable containing the headers of this message.
        /// </summary>
        public ImmutableList<HttpHeader> Headers { get; }

        /// <summary>
        /// Returns the entity of this request.
        /// </summary>
        public override ResponseEntity Entity { get; }

        public string Protocol { get; }

        /// <summary>
        /// Returns a default response to be changed using the `WithX` methods.
        /// </summary>
        public static HttpResponse Create(int status = 200, ImmutableList<HttpHeader> headers = null, ResponseEntity entity = null, string protocol = "HTTP/1.1") =>
            new HttpResponse(status, headers, entity ?? ResponseEntity.Empty, protocol);

        private HttpResponse(int status, ImmutableList<HttpHeader> headers, ResponseEntity entity, string protocol)
        {
            Status = status;
            Headers = headers;
            Entity = entity;
            Protocol = protocol;
        }

        /// <inheritdoc />
        public override HttpResponse WithEntity(RequestEntity entity) => Copy(entity: entity);

        /// <summary>
        /// Returns a copy of this instance with a new status-code.
        /// </summary>
        public HttpResponse WithStatus(int statusCode) => Copy(statusCode);

        private HttpResponse Copy(
            int? status = null, 
            ImmutableList<HttpHeader> headers = null, 
            ResponseEntity entity = null,
            string protocol = null) => 
            new HttpResponse(status ?? Status, headers ?? Headers, entity ?? Entity, protocol ?? Protocol);

        private bool Equals(HttpResponse other) => Status == other.Status &&
           Equals(Headers, other.Headers) &&
           Equals(Entity, other.Entity) &&
           Protocol == other.Protocol;

        public override bool Equals(object obj) => 
            ReferenceEquals(this, obj) || obj is HttpResponse other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Status;
                hashCode = (hashCode * 397) ^ (Headers != null ? Headers.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Entity != null ? Entity.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Protocol != null ? Protocol.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
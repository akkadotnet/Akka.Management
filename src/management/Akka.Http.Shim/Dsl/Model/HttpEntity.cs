using Akka.Annotations;
using Akka.IO;

namespace Akka.Http.Dsl.Model
{
    /// <summary>
    /// Represents the entity of an Http message.
    /// An entity consists of the content-type of the data and the actual data itself.
    /// </summary>
    [DoNotInherit]
    public abstract class HttpEntity
    {
        /// <summary>
        /// The `ContentType` associated with this entity.
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// The data of this entity.
        /// </summary>
        public ByteString DataBytes { get; }

        /// <summary>
        /// Creates a copy of this HttpEntity with the `contentType` overridden with the given one.
        /// </summary>
        public abstract HttpEntity WithContentType(string contentType);

        public static HttpEntity Empty => new RequestEntity(null, ByteString.Empty);

        public static HttpEntity Create(string content) => Create("text/plain(UTF-8)", content);

        public static HttpEntity Create(ByteString data) => Create("application/octet-stream", data);

        public static HttpEntity Create(string contentType, string content) =>
            string.IsNullOrEmpty(content) ? Empty : Create(contentType, ByteString.FromString(content));

        public static HttpEntity Create(string contentType, ByteString data) =>
            new RequestEntity(contentType, data);

        protected HttpEntity(string contentType, ByteString dataBytes)
        {
            ContentType = contentType;
            DataBytes = dataBytes;
        }
    }

    /// <summary>
    /// An <see cref="HttpEntity"/> that can be used for requests.
    /// Note that all entities that can be used for requests can also be used for responses (but not the other way around).
    /// </summary>
    public class RequestEntity : ResponseEntity
    {
        public RequestEntity(string contentType, ByteString dataBytes)
            : base(contentType, dataBytes)
        { }
    }

    /// <summary>
    /// An <see cref="HttpEntity"/> that can be used for responses.
    /// Note that all entities that can be used for requests can also be used for responses (but not the other way around).
    /// </summary>
    public class ResponseEntity : HttpEntity
    {
        public new static ResponseEntity Empty => new ResponseEntity(null, ByteString.Empty);

        public ResponseEntity(string contentType, ByteString dataBytes)
            : base(contentType, dataBytes)
        { }

        public override HttpEntity WithContentType(string contentType) =>
            contentType == ContentType ? this : Copy(contentType);

        private ResponseEntity Copy(string contentType = null, ByteString data = null) =>
            new ResponseEntity(contentType ?? ContentType, data ?? DataBytes);
    }
}
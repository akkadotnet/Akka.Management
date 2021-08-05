namespace Akka.Http.Dsl.Model
{
    /// <summary>
    /// Contains the set of predefined content-types for convenience.
    /// </summary>
    public static class ContentTypes
    {
        public static readonly string ApplicationJson = "application/json";
        public static readonly string ApplicationOctetStream = "application/octet-stream";
        public static readonly string ApplicationXWwwFormUrlencoded = "application/x-www-form-urlencoded";
        public static readonly string TextPlainUtf8 = "text/plain;charset=utf-8";
        public static readonly string TextHtmlUtf8 = "text/html;charset=utf-8";
        public static readonly string TextXmlUtf8 = "text/xml;charset=utf-8";
        public static readonly string TextCsvUtf8 = "text/csv;charset=utf-8";
    }
}
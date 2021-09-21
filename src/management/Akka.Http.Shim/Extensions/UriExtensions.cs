namespace System
{
    public static class UriExtensions
    {
        public static Uri WithPort(this Uri uri, int newPort)
        {
            var builder = new UriBuilder(uri) { Port = newPort };
            return builder.Uri;
        }
    }
}
using System;
using System.IO;

namespace NuGet.Common
{
    public static class UriUtility
    {
        /// <summary>
        /// Same as "new Uri" except that it can handle UNIX style paths that start with '/'
        /// </summary>
        public static Uri CreateSourceUri(string source, UriKind kind = UriKind.Absolute)
        {
            source = FixSourceUri(source);
            return new Uri(source, kind);
        }

        /// <summary>
        /// Same as "Uri.TryCreate" except that it can handle UNIX style paths that start with '/'
        /// </summary>
        public static Uri TryCreateSourceUri(string source, UriKind kind)
        {
            source = FixSourceUri(source);

            Uri uri;
            return Uri.TryCreate(source, kind, out uri) ? uri : null;
        }

        private static string FixSourceUri(string source)
        {
            // UNIX absolute paths need to start with file://
            if (Path.DirectorySeparatorChar == '/' && !string.IsNullOrEmpty(source) && source[0] == '/')
            {
                source = "file://" + source;
            }

            return source;
        }

        /// <summary>
        /// Provides Uri encoding for V2 servers in the same way that NuGet.Core.dll encoded urls.
        /// </summary>
        public static string UrlEncodeOdataParameter(string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                // OData requires that a single quote MUST be escaped as 2 single quotes.
                // In .NET 4.5, Uri.EscapeDataString() escapes single quote as %27. Thus we must replace %27 with 2 single quotes.
                // In .NET 4.0, Uri.EscapeDataString() doesn't escape single quote. Thus we must replace it with 2 single quotes.
                return Uri.EscapeDataString(value).Replace("'", "''").Replace("%27", "''");
            }

            return value;
        }
    }
}

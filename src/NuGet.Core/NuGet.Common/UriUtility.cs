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
    }
}

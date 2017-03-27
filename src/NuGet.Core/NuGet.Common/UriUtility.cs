// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Common
{
    public static class UriUtility
    {
        private const string FilePrefix = "file://";

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
                source = FilePrefix + source;
            }

            return source;
        }

        /// <summary>
        /// Provides Uri encoding for V2 servers in the same way that NuGet.Core.dll encoded urls.
        /// </summary>
        public static string UrlEncodeOdataParameter(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                // OData requires that a single quote MUST be escaped as 2 single quotes.
                // In .NET 4.5, Uri.EscapeDataString() escapes single quote as %27. Thus we must replace %27 with 2 single quotes.
                // In .NET 4.0, Uri.EscapeDataString() doesn't escape single quote. Thus we must replace it with 2 single quotes.
                return Uri.EscapeDataString(value).Replace("'", "''").Replace("%27", "''");
            }

            return value;
        }

        /// <summary>
        /// Convert a file:// URI to a local path.
        /// </summary>
        /// <returns>If the input can be parsed this will return Uri.LocalPath, if the input 
        /// is not a URI or fails to parse the original string will be returned.</returns>
        /// <param name="localOrUriPath">Possible file:// URI path or local path.</param>
        public static string GetLocalPath(string localOrUriPath)
        {
            // check if this starts with file://
            if (!string.IsNullOrEmpty(localOrUriPath)
                && localOrUriPath.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                // convert to a uri and get the local path
                Uri uri;
                if (Uri.TryCreate(localOrUriPath, UriKind.RelativeOrAbsolute, out uri))
                {
                    return uri.LocalPath;
                }
            }

            // Return the same path
            return localOrUriPath;
        }
    }
}

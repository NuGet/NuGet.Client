using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetClient.Test.Foundation.Utility.IO
{
    public static class PathResolver
    {
        private static readonly Dictionary<string, string> ResolvedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static PathResolver()
        {
            PathResolver.ResolvedPaths.Add(string.Empty, string.Empty);
        }

        /// <summary>
        /// Normalizes a given path. For example, C:\A\.\B would be resolved to C:\A\B.
        /// Additionally, 8.3 format short names like "TE021F~1.TXT" are expanded into the long form.
        /// </summary>
        /// <remarks>
        /// If the given path is not rooted, i.e. does not begin with C:\ or \\server\share\, then
        /// the current working directory is prepended to the path.
        /// </remarks>
        public static string ResolvePath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            string resolvedPath;
            if (PathResolver.ResolvedPaths.TryGetValue(path, out resolvedPath))
            {
                return resolvedPath;
            }

            resolvedPath = PathResolver.ResolvePathInternal(path);

            try
            {
                // Check for directory for local paths only.
                PathFormat pathFormat = PathHelper.GetPathFormat(resolvedPath);
                if (PathHelper.IsUncJustServerShare(pathFormat, resolvedPath)
                    || (pathFormat == PathFormat.DriveAbsolute && PathHelper.IsDirectory(resolvedPath)))
                {
                    // Ensure a trailing separator on paths that we *know* are directories.
                    // This allows some level of optimization in dealing with the resulting path.
                    resolvedPath = PathHelper.EnsurePathEndsInDirectorySeparator(resolvedPath);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // We may not be able to tell if the path in question is a directory, since we got
                // the full path and this is just an attempt to optimize further calls we can consider
                // ourselves "ok"
                Debug.WriteLine("Unauthorized access attempting to find directory state for '{0}'", resolvedPath);
            }
            finally
            {
                PathResolver.ResolvedPaths.Add(path, resolvedPath);
            }

            return resolvedPath;
        }

        private static string ResolvePathInternal(string path)
        {
            string resolvedPath;
            if (PathHelper.TryValidateAndFixPath(path, out resolvedPath))
            {
                string fullPath = PathResolver.GetFullPath(resolvedPath, AccessHelper.AccessService.MiscGetFullPathName);
                if (fullPath != null)
                {
                    resolvedPath = fullPath;
                }
            }

            return resolvedPath;
        }

        private static string GetFullPath(string path, Func<string, string> longPathFallback)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "Unabled to resolve path: {0}", path));
            }
            catch (NotSupportedException)
            {
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "Unabled to resolve path: {0}", path));
            }
            catch (PathTooLongException)
            {
                // Path.GetFullPath cannot handle long paths (\\?\) to replicate the behavior
                // we would need to call Win32's GetFullPathName and walk the directory tree to
                // resolve long filenames, e.g. convert DIRECT~1 to Directory. This would be
                // necessary as GetLongPathName does NOT work with paths that do not exist.
                if (longPathFallback != null)
                {
                    string longPathResult = longPathFallback(path);

                    // Path.GetFullPath does not handle resolving relative paths that start out longer than MAX_PATH, but
                    // ultimately are shorter than MAX_PATH. We'll use our own GetFullPathName and will reattempt the
                    // resolve one more time.
                    return PathResolver.GetFullPath(longPathResult, longPathFallback: null);
                }
                else
                {
                    Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "Unable to resolve long path: {0}", path));
                }
            }

            return null;
        }
    }
}

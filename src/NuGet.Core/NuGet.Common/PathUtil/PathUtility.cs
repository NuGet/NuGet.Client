using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Common
{
    public static class PathUtility
    {
        private static readonly Lazy<bool> _isFileSystemCaseInsensitive = new Lazy<bool>(CheckIfFileSystemIsCaseInsensitive);
        /// <summary>
        /// Returns OrdinalIgnoreCase windows and mac. Ordinal for linux.
        /// </summary>
        /// <returns></returns>
        public static StringComparer GetStringComparerBasedOnOS()
        {
            if (IsFileSystemCaseInsensitive)
            {
                return StringComparer.OrdinalIgnoreCase;
            }

            return StringComparer.Ordinal;
        }

        /// <summary>
        /// Returns distinct orderd paths based on the file system case sensitivity.
        /// </summary>
        public static IEnumerable<string> GetUniquePathsBasedOnOS(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            var unique = new HashSet<string>(GetStringComparerBasedOnOS());

            foreach (var path in paths)
            {
                if (unique.Add(path))
                {
                    yield return path;
                }
            }

            yield break;
        }

        public static string GetPathWithForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0
                || path[path.Length - 1] == trailingCharacter)
            {
                return path;
            }

            return path + trailingCharacter;
        }

        public static bool IsFileSystemCaseInsensitive
        {
            get { return _isFileSystemCaseInsensitive.Value; }
        }

        private static bool CheckIfFileSystemIsCaseInsensitive()
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                return true;
            }
            else
            {
                var listOfPathsToCheck = new string[]
                {
                    NuGetEnvironment.GetFolderPath(NuGetFolderPath.NuGetHome),
                    NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp),
                    Directory.GetCurrentDirectory()
                };

                var isCaseInsensitive = true;
                foreach (var path in listOfPathsToCheck)
                {
                    bool ignore;
                    var result = CheckCaseSenstivityRecursivelyTillDirectoryExists(path, out ignore);
                    if (!ignore)
                    {
                        isCaseInsensitive &= result;
                    }
                }
                return isCaseInsensitive;
            }
        }

        private static bool CheckCaseSenstivityRecursivelyTillDirectoryExists(string path, out bool ignoreResult)
        {
            var parentDirectoryFound = true;
            path = Path.GetFullPath(path);
            ignoreResult = true;
            while (true)
            {
                if (path.Length <= 1)
                {
                    ignoreResult = true;
                    parentDirectoryFound = false;
                    break;
                }
                if (Directory.Exists(path))
                {
                    ignoreResult = false;
                    break;
                }
                path = Path.GetDirectoryName(path);
            }

            if (parentDirectoryFound)
            {
                return Directory.Exists(path.ToLowerInvariant()) && Directory.Exists(path.ToUpperInvariant());
            }
            return false;
        }

    }
}
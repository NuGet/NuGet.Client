using System;
using System.IO;

namespace NuGet.Common
{
    public static class PathUtility
    {
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
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static class ProjectHelper
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private static readonly HashSet<string> _supportedProjectExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".csproj",
            ".vbproj",
            ".fsproj",
            ".btproj",
            ".vcxproj",
            ".jsproj",
            ".wixproj",
            ".nuproj",
            ".nfproj",
        };

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static HashSet<string> SupportedProjectExtensions
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                return _supportedProjectExtensions;
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool TryGetProjectFile(string directory, out string projectFile)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            projectFile = null;
            var candidates = GetProjectFiles(directory).ToArray();
            if (candidates.Length == 1)
            {
                projectFile = candidates[0];
            }

            return !String.IsNullOrEmpty(projectFile);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static IEnumerable<string> GetProjectFiles(string directory)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return _supportedProjectExtensions.SelectMany(x => Directory.GetFiles(directory, "*" + x));
        }


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static string GetSolutionDir(string projectDirectory)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            string path = projectDirectory;

            // Only look 4 folders up to find the solution directory
            const int maxDepth = 5;
            int depth = 0;
            do
            {
                if (SolutionFileExists(path))
                {
                    return path;
                }

                path = Path.GetDirectoryName(path);

                depth++;
                //When you get to c:\, the parent path is null.
            } while (depth < maxDepth && path != null);

            return null;
        }

        private static bool SolutionFileExists(string path)
        {
            return Directory.GetFiles(path, "*.sln").Any();
        }
    }
}

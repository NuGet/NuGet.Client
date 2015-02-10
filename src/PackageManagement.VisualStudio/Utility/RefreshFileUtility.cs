using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class RefreshFileUtility
    {
        private const string RefreshFileExtension = ".refresh";

        /// <summary>
        /// Creates a .refresh file in bin directory of the IFileSystem that points to the assembly being installed. 
        /// This works around issues in DTE's AddReference method when dealing with GACed binaries.
        /// </summary>
        /// <param name="root">the root path is dte full path</param>
        /// <param name="assemblyPath">The relative path to the assembly being added</param>
        public static void CreateRefreshFile(string root, string assemblyPath)
        {
            string referenceName = Path.GetFileName(assemblyPath);
            string refreshFilePath = Path.Combine("bin", referenceName + RefreshFileExtension);
            if (!FileSystemUtility.FileExists(root, refreshFilePath))
            {
                string projectPath = PathUtility.EnsureTrailingSlash(root);
                string relativeAssemblyPath = PathUtility.GetRelativePath(projectPath, assemblyPath);

                try
                {
                    using (var stream = StreamUtility.StreamFromString(relativeAssemblyPath))
                    {
                        FileSystemUtility.AddFile(root,refreshFilePath, stream);
                    }
                }
                catch (UnauthorizedAccessException exception)
                {
                    // log IO permission error
                    ExceptionHelper.WriteToActivityLog(exception);
                }
            }
        }

        public static IEnumerable<string> ResolveRefreshPaths(string root)
        {
            // Resolve all .refresh files from the website's bin directory. Once resolved, verify the path exists on disk and that they look like an assembly reference. 
            return from file in FileSystemUtility.GetFiles(root, "bin", "*" + RefreshFileExtension, recursive: false)
                   let resolvedPath = SafeResolveRefreshPath(root, file)
                   where resolvedPath != null &&
                         FileSystemUtility.FileExists(root, resolvedPath) &&
                         Constants.AssemblyReferencesExtensions.Contains(Path.GetExtension(resolvedPath))
                   select resolvedPath;
        }

        private static string SafeResolveRefreshPath(string root, string file)
        {
            string relativePath;
            try
            {
                using (var stream = File.OpenRead(FileSystemUtility.GetFullPath(root, file)))
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        relativePath = streamReader.ReadToEnd();
                    }
                }
                return FileSystemUtility.GetFullPath(root, relativePath);
            }
            catch
            {
                // Ignore the .refresh file if it cannot be read.
            }
            return null;
        }
    }
}

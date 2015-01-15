using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    internal static class RefreshFileUtility
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
                    using (var stream = StreamUtility.AsStream(relativeAssemblyPath))
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
    }
}

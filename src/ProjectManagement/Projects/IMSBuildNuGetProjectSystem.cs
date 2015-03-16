using NuGet.Frameworks;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public interface IMSBuildNuGetProjectSystem
    {
        NuGetFramework TargetFramework { get; }
        string ProjectName { get; }
        string ProjectUniqueName { get; }
        string ProjectFullPath { get;}
        INuGetProjectContext NuGetProjectContext { get; }
        void SetNuGetProjectContext(INuGetProjectContext nuGetProjectContext);
        void AddFile(string path, Stream stream);
        void AddExistingFile(string path);
        void RemoveFile(string path);
        bool FileExistsInProject(string path);
        /// <summary>
        /// Method called when adding an assembly reference to the project.
        /// </summary>
        /// <param name="referencePath">Physical path to the assembly file relative to the project root.</param>
        void AddReference(string referencePath);
        void RemoveReference(string name);
        bool ReferenceExists(string name);
        /// <summary>
        /// Adds an assembly reference to a framework assembly (one in the GAC).
        /// </summary>
        /// <param name="name">name of the assembly</param>
        void AddFrameworkReference(string name);
        void AddImport(string targetFullPath, ImportLocation location);
        void RemoveImport(string targetFullPath);
        dynamic GetPropertyValue(string propertyName);
        string ResolvePath(string path);
        bool IsSupportedFile(string path);
        void AddBindingRedirects();
        Task ExecuteScriptAsync(string packageInstallPath, string scriptRelativePath, ZipArchive packageZipArchive, NuGetProject nuGetProject);
        void BeginProcessing(IEnumerable<string> files);
        void EndProcessing();

        void DeleteDirectory(string path, bool recursive);

        // The returned file names are relative paths.
        IEnumerable<string> GetFiles(string path, string filter, bool recursive);

        /// <summary>
        /// Returns the directories under the directory <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The directory under which to search for subdirectories.</param>
        /// <returns>The list of subdirectories in relative path.</returns>
        IEnumerable<string> GetDirectories(string path);

        // LIKELY, THERE HAS TO MORE STUFF HERE like 'IsSupportedFile' and 'IsBindingRedirectsEnabled'
        // IMO, there are hacks introduced to special case based on project systems like 'websites' and 'silverlight'
    }

    public enum ImportLocation
    {
        Top,
        Bottom
    }
}

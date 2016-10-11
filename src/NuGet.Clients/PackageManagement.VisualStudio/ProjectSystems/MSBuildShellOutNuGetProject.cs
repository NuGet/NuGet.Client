// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An implementation of <see cref="NuGetProject"/> that shells out to MSBuild to execute the NuGet.Build.Tasks
    /// on an arbitrary project. The build task constructs the dependency graph spec on disk. This implementation
    /// reads that file, extracting the needed information for restore.
    /// </summary>
    public class MSBuildShellOutNuGetProject : NuGetProject, INuGetIntegratedProject, IDependencyGraphProject
    {
        /// <summary>
        /// How long to wait for MSBuild to finish when shelling out. 2 minutes in milliseconds.
        /// </summary>
        private const int MSBuildWaitTime = 2 * 60 * 1000;

        /// <summary>
        /// The MSBuild property for the base intermediate output path. This is "obj" by default.
        /// </summary>
        private const string BaseIntermediateOutputPathName = "BaseIntermediateOutputPath";

        private const string DefaultBaseIntermediateOutputPath = "obj";

        /// <summary>
        /// The MSBuild property for the target frameworks supported by the project. The presence of this property
        /// indicates that the project is an <code>PackageReference</code>-based NuGet project.
        /// </summary>
        private const string TargetFrameworksName = "TargetFrameworks";

        /// <summary>
        /// Single Framework
        /// </summary>
        private const string TargetFrameworkName = "TargetFramework";

        /// <summary>
        /// The MSBuild property for the path to MSBuild.
        /// </summary>
        private const string MSBuildToolsPathName = "MSBuildToolsPath";

        private const string MSBuildExe = "msbuild.exe";

        private static readonly string MSBuild14Path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"MSBuild\14.0\Bin",
                MSBuildExe);
        private static readonly string MSBuild15Path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"MSBuild\15.0\Bin",
                MSBuildExe);

        private readonly IVsBuildPropertyStorage _buildPropertyStorage;
        private readonly string _fullProjectPath;
        private readonly string _projectName;
        private readonly string _projectFullPath;
        private readonly string _projectUniqueName;
        private readonly string _msbuildPath;

        /// <summary>
        /// Cache the installed packages list. This is refreshed every time a restore happens.
        /// </summary>
        private readonly object _installedPackagesLock = new object();
        private List<PackageReference> _installedPackages = null;

        public static MSBuildShellOutNuGetProject Create(EnvDTEProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // The project must be an IVSHierarchy.
            var hierarchy = VsHierarchyUtility.ToVsHierarchy(project);
            if (hierarchy == null)
            {
                return null;
            }

            // The project must be an IVsBuildPropertyStorage.
            var buildPropertyStorage = hierarchy as IVsBuildPropertyStorage;
            if (buildPropertyStorage == null)
            {
                return null;
            }

            // MSBuild must be found.
            var msbuildPath = GetMSBuildPath(buildPropertyStorage, project.DTE);
            if (msbuildPath == null)
            {
                return null;
            }

            // The project must have the "TargetFrameworks" or "TargetFramework" property.
            if (GetMSBuildProperty(buildPropertyStorage, TargetFrameworksName) == null
                && GetMSBuildProperty(buildPropertyStorage, TargetFrameworkName) == null)
            {
                return null;
            }

            // Get information about the project from DTE.
            var fullProjectPath = EnvDTEProjectUtility.GetFullProjectPath(project);
            var projectName = project.Name;
            var projectFullPath = EnvDTEProjectUtility.GetFullPath(project);
            var projectUniqueName = EnvDTEProjectUtility.GetUniqueName(project);

            return new MSBuildShellOutNuGetProject(
                buildPropertyStorage,
                fullProjectPath,
                projectName,
                projectFullPath,
                projectUniqueName,
                msbuildPath);
        }

        private MSBuildShellOutNuGetProject(
            IVsBuildPropertyStorage buildPropertyStorage,
            string fullProjectPath,
            string projectName,
            string projectFullPath,
            string projectUniqueName,
            string msbuildPath)
        {
            _buildPropertyStorage = buildPropertyStorage;
            _fullProjectPath = fullProjectPath;
            _projectName = projectName;
            _projectFullPath = projectFullPath;
            _projectUniqueName = projectUniqueName;
            _msbuildPath = msbuildPath;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, _projectFullPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _projectUniqueName);
        }

        public string BaseIntermediateOutputPath
        {
            get
            {
                var relativePath = ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return GetMSBuildProperty(_buildPropertyStorage, BaseIntermediateOutputPathName);
                });

                var absolutePath = Path.GetFullPath(Path.Combine(
                    _projectFullPath,
                    relativePath ?? DefaultBaseIntermediateOutputPath));

                return absolutePath;
            }
        }

        public string MSBuildProjectPath => _projectFullPath;

        /// <summary>
        /// Making this timestamp as the current time means that a restore with this project in the graph
        /// will never no-op. We do this to keep this work-around implementation simple.
        /// </summary>
        public DateTimeOffset LastModified => DateTimeOffset.Now;

        private static string GetMSBuildProperty(IVsBuildPropertyStorage buildPropertyStorage, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            
            string output;
            var result = buildPropertyStorage.GetPropertyValue(
                name,
                string.Empty,
                (uint)_PersistStorageType.PST_PROJECT_FILE,
                out output);

            if (result != NuGetVSConstants.S_OK || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            return output;
        }

        public IReadOnlyList<PackageSpec> GetPackageSpecsForRestore(ExternalProjectReferenceContext referenceContext)
        {
            var specs = GetSpecsAndInitializeInstalledPackages();

            if (specs.Package == null)
            {
                throw new InvalidOperationException($"No package spec was found for project '{_fullProjectPath}'.");
            }

            return specs.Tools.Concat(new[] { specs.Package }).ToList();
        }

        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            // Switch to a background thread for this work.
            await TaskScheduler.Default;

            var installedPackages = _installedPackages;
            if (installedPackages != null)
            {
                return installedPackages;
            }

            lock (_installedPackagesLock)
            {
                installedPackages = _installedPackages;
                if (installedPackages != null)
                {
                    return installedPackages;
                }

                GetSpecsAndInitializeInstalledPackages();
                return _installedPackages;
            }
        }

        private Specs GetSpecsAndInitializeInstalledPackages()
        {
            var dependencyGraphSpec = MSBuildUtility.GetProjectReferences(
                _msbuildPath,
                new[] { _fullProjectPath },
                MSBuildWaitTime);

            var packageSpec = dependencyGraphSpec
                .Projects
                .FirstOrDefault(p => _fullProjectPath.Equals(
                    p.RestoreMetadata.ProjectPath,
                    StringComparison.OrdinalIgnoreCase));

            if (packageSpec != null)
            {
                // Set the output path, since shelling out to MSBuild does not set this.
                packageSpec.RestoreMetadata.OutputPath = BaseIntermediateOutputPath;

                // Update the list of references while we have the package spec.
                lock (_installedPackagesLock)
                {
                    _installedPackages = GetPackageReferences(packageSpec);
                }
            }
            else
            {
                lock (_installedPackagesLock)
                {
                    _installedPackages = new List<PackageReference>();
                }
            }

            var spec = new Specs
            {
                DependencyGraph = dependencyGraphSpec,
                Package = packageSpec
            };

            // The only tools in the dg file should be the top level dependencies, add all of them
            spec.Tools.AddRange(dependencyGraphSpec
                .Projects
                .Where(p => p.RestoreMetadata.OutputType == RestoreOutputType.DotnetCliTool));

            return spec;
        }

        private List<PackageReference> GetPackageReferences(PackageSpec packageSpec)
        {
            if (packageSpec == null)
            {
                return new List<PackageReference>();
            }

            var frameworkSorter = new NuGetFrameworkSorter();

            return packageSpec
                .TargetFrameworks
                .SelectMany(f => MapLibrariesToPackages(f.FrameworkName, f.Dependencies))
                .GroupBy(p => p.PackageIdentity)
                .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
                .ToList();
        }

        public override Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> UninstallPackageAsync(
            PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<PackageReference> MapLibrariesToPackages(NuGetFramework targetFramework, IEnumerable<LibraryDependency> libraries)
        {
            foreach (var library in libraries)
            {
                if (library.LibraryRange.TypeConstraint != LibraryDependencyTarget.Package)
                {
                    continue;
                }

                var identity = new PackageIdentity(
                    library.LibraryRange.Name,
                    library.LibraryRange.VersionRange.MinVersion);

                yield return new PackageReference(identity, targetFramework);
            }
        }

        public static string GetMSBuildPath(IVsBuildPropertyStorage buildPropertyStorage, EnvDTE.DTE dte)
        {
            string msbuildPath;

            // Try to get the MSBuild tools path from the project itself.
            var msbuildToolsPath = GetMSBuildProperty(buildPropertyStorage, MSBuildToolsPathName);
            if (msbuildToolsPath != null)
            {
                msbuildPath = Path.Combine(Path.GetFullPath(msbuildToolsPath), MSBuildExe);
                
                if (File.Exists(msbuildPath))
                {
                    return msbuildPath;
                }
            }

            // Detect the MSBuild directory based on the Visual Studio version number.
            if (dte.Version.StartsWith("15."))
            {
                msbuildPath = MSBuild15Path;
            }
            else
            {
                msbuildPath = MSBuild14Path;
            }

            if (File.Exists(msbuildPath))
            {
                return msbuildPath;
            }

            // Try to get MSBuild from the PATH.
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            var pathDirectories = pathVariable.Split(Path.PathSeparator);
            foreach (var pathDirectory in pathDirectories)
            {
                msbuildPath = Path.Combine(pathDirectory, MSBuildExe);

                if (File.Exists(msbuildPath))
                {
                    return msbuildPath;
                }
            }

            return null;
        }

        public bool IsRestoreRequired(
            IEnumerable<VersionFolderPathResolver> pathResolvers,
            ISet<PackageIdentity> packagesChecked,
            ExternalProjectReferenceContext context)
        {
            // TODO: when the real implementation of NuGetProject for CPS PackageReference is completed, more
            // sophisticated restore no-op detection logic is required. Always returning true means that every build
            // will result in a restore.
            return true;
        }

        public async Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
            ExternalProjectReferenceContext context)
        {
            await TaskScheduler.Default;

            var specs = GetSpecsAndInitializeInstalledPackages();

            var externalProjectReferences = new HashSet<ExternalProjectReference>();

            foreach (var packageSpec in specs.DependencyGraph.Projects)
            {
                var projectReferences = packageSpec
                    .RestoreMetadata
                    .TargetFrameworks
                    .SelectMany(e => e.ProjectReferences)
                    .Select(r => r.ProjectUniqueName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var reference = new ExternalProjectReference(
                    packageSpec.RestoreMetadata.ProjectPath,
                    packageSpec,
                    packageSpec.RestoreMetadata.ProjectPath,
                    projectReferences);

                externalProjectReferences.Add(reference);
            }

            return DependencyGraphProjectCacheUtility
                .GetExternalClosure(_fullProjectPath, externalProjectReferences)
                .ToList();
        }

        private class Specs
        {
            public DependencyGraphSpec DependencyGraph { get; set; }
            public PackageSpec Package { get; set; }
            public List<PackageSpec> Tools { get; set; } = new List<PackageSpec>();
        }

        private static class MSBuildUtility
        {
            public static DependencyGraphSpec GetProjectReferences(
                string msbuildPath,
                string[] projectPaths,
                int timeOut)
            {
                if (msbuildPath == null)
                {
                    throw new ArgumentNullException(nameof(msbuildPath));
                }

                if (!File.Exists(msbuildPath))
                {
                    throw new InvalidOperationException("msbuild.exe could not be found.");
                }

                var buildTasksDirectory = Path.GetDirectoryName(typeof(MSBuildUtility).Assembly.Location);
                var buildTasksPath = Path.Combine(buildTasksDirectory, "NuGet.Build.Tasks.dll");
                
                if (!File.Exists(buildTasksPath))
                {
                    throw new InvalidOperationException("NuGet.Build.Tasks.dll could not be found.");
                }

                // The targets file should be part of the VSIX
                var entryPointTargetPath = Path.Combine(buildTasksDirectory, "NuGet.targets");

                if (!File.Exists(entryPointTargetPath))
                {
                    throw new InvalidOperationException($"{entryPointTargetPath} could not be found.");
                }

                using (var resultsPath = new TempFile(".result"))
                {
                    var argumentBuilder = new StringBuilder(
                        "/t:GenerateRestoreGraphFile " +
                        "/nologo /nr:false " +
                        "/p:BuildProjectReferences=false");

                    // Set the msbuild verbosity level if specified
                    var msbuildVerbosity = Environment.GetEnvironmentVariable("NUGET_RESTORE_MSBUILD_VERBOSITY");

                    if (string.IsNullOrEmpty(msbuildVerbosity))
                    {
                        argumentBuilder.Append(" /v:q ");
                    }
                    else
                    {
                        argumentBuilder.Append($" /v:{msbuildVerbosity} ");
                    }

                    // Add additional args to msbuild if needed
                    var msbuildAdditionalArgs = Environment.GetEnvironmentVariable("NUGET_RESTORE_MSBUILD_ARGS");

                    if (!string.IsNullOrEmpty(msbuildAdditionalArgs))
                    {
                        argumentBuilder.Append($" {msbuildAdditionalArgs} ");
                    }

                    // Disallow the import of targets/props from packages
                    argumentBuilder.Append(" /p:ExcludeRestorePackageImports=true ");

                    argumentBuilder.Append(" /p:RestoreTaskAssemblyFile=");
                    AppendQuoted(argumentBuilder, buildTasksPath);

                    argumentBuilder.Append(" /p:RestoreGraphOutputPath=");
                    AppendQuoted(argumentBuilder, resultsPath);

                    argumentBuilder.Append(" /p:RestoreGraphProjectInput=\"");
                    for (var i = 0; i < projectPaths.Length; i++)
                    {
                        argumentBuilder.Append(projectPaths[i])
                            .Append(";");
                    }

                    argumentBuilder.Append("\" ");
                    AppendQuoted(argumentBuilder, entryPointTargetPath);

                    var processStartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = msbuildPath,
                        Arguments = argumentBuilder.ToString(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(processStartInfo))
                    {
                        var finished = process.WaitForExit(timeOut);

                        if (!finished)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException("msbuild.exe could not be killed.", ex);
                            }

                            throw new InvalidOperationException("msbuild.exe timed out.");
                        }

                        if (process.ExitCode != 0)
                        {
                            throw new InvalidOperationException(
                                "msbuild.exe failed. STDOUT:" + Environment.NewLine +
                                process.StandardOutput.ReadToEnd() + Environment.NewLine +
                                "STDERR:" + Environment.NewLine +
                                process.StandardError.ReadToEnd());
                        }
                    }

                    DependencyGraphSpec spec = null;

                    if (File.Exists(resultsPath))
                    {
                        spec = DependencyGraphSpec.Load(resultsPath);
                        File.Delete(resultsPath);
                    }
                    else
                    {
                        spec = new DependencyGraphSpec();
                    }

                    return spec;
                }
            }

            private static void AppendQuoted(StringBuilder builder, string targetPath)
            {
                builder
                    .Append('"')
                    .Append(targetPath)
                    .Append('"');
            }

            private static void ExtractResource(string resourceName, string targetPath)
            {
                using (var input = typeof(MSBuildUtility).Assembly.GetManifestResourceStream(resourceName))
                {
                    using (var output = File.OpenWrite(targetPath))
                    {
                        input.CopyTo(output);
                    }
                }
            }

            /// <summary>
            /// This class is used to create a temp file, which is deleted in Dispose().
            /// </summary>
            private class TempFile : IDisposable
            {
                private readonly string _filePath;

                /// <summary>
                /// Constructor. It creates an empty temp file under the temp directory / NuGet, with
                /// extension <paramref name="extension"/>.
                /// </summary>
                /// <param name="extension">The extension of the temp file.</param>
                public TempFile(string extension)
                {
                    if (string.IsNullOrEmpty(extension))
                    {
                        throw new ArgumentNullException(nameof(extension));
                    }

                    var tempDirectory = Path.Combine(Path.GetTempPath(), "NuGet-Scratch");

                    Directory.CreateDirectory(tempDirectory);

                    int count = 0;
                    do
                    {
                        _filePath = Path.Combine(tempDirectory, Path.GetRandomFileName() + extension);

                        if (!File.Exists(_filePath))
                        {
                            try
                            {
                                // create an empty file
                                using (var filestream = File.Open(_filePath, FileMode.CreateNew))
                                {
                                }

                                // file is created successfully.
                                return;
                            }
                            catch
                            {
                            }
                        }

                        count++;
                    }
                    while (count < 3);

                    throw new InvalidOperationException("Failed to create a random file name.");
                }

                public static implicit operator string(TempFile f)
                {
                    return f._filePath;
                }

                public void Dispose()
                {
                    if (File.Exists(_filePath))
                    {
                        try
                        {
                            File.Delete(_filePath);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
    }
}

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
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    public class MSBuildShellOutNuGetProject : NuGetProject, INuGetIntegratedProject
    {
        private const string MsBuild14Directory = @"C:\Program Files (x86)\MSBuild\14.0\Bin";
        private const string MsBuild15Directory = @"C:\Program Files (x86)\MSBuild\15.0\Bin";
        private const int MsBuildWaitTime = 2 * 60 * 1000; // 2 minutes in milliseconds

        private readonly EnvDTEProject _envDTEProject;
        private readonly IVsBuildPropertyStorage _buildPropertyStorage;

        private readonly string _fullProjectPath;
        private readonly string _projectName;
        private readonly string _projectFullPath;
        private readonly string _projectUniqueName;
        private readonly string _msBuildDirectory;

        private readonly object _installedPackagesLock = new object();
        private List<PackageReference> _installedPackages;

        public MSBuildShellOutNuGetProject(EnvDTEProject envDTEProject, IVsBuildPropertyStorage buildPropertyStorage)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _envDTEProject = envDTEProject;
            _buildPropertyStorage = buildPropertyStorage;

            // Get information about the project from DTE.
            _fullProjectPath = EnvDTEProjectUtility.GetFullProjectPath(_envDTEProject);
            _projectName = _envDTEProject.Name;
            _projectFullPath = EnvDTEProjectUtility.GetFullPath(_envDTEProject);
            _projectUniqueName = EnvDTEProjectUtility.GetUniqueName(_envDTEProject);

            // Detect the MSBuild directory based on the Visual Studio version number.
            if (_envDTEProject.DTE.Version.StartsWith("15."))
            {
                _msBuildDirectory = MsBuild15Directory;
            }
            else
            {
                _msBuildDirectory = MsBuild14Directory;
            }

            // Add project metadata.
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

                    string output;
                    var result = _buildPropertyStorage.GetPropertyValue(
                        "BaseIntermediateOutputPath",
                        string.Empty,
                        (uint)_PersistStorageType.PST_PROJECT_FILE,
                        out output);

                    await TaskScheduler.Current;

                    if (result != NuGetVSConstants.S_OK || string.IsNullOrWhiteSpace(output))
                    {
                        return null;
                    }

                    return output;
                });

                var absolutePath = Path.GetFullPath(Path.Combine(_projectFullPath, relativePath));

                return absolutePath;
            }
        }

        public PackageSpec GetPackageSpecForRestore()
        {
            var dgSpec = MsBuildUtility.GetProjectReferences(
                _msBuildDirectory,
                new[] { _fullProjectPath },
                MsBuildWaitTime);

            var packageSpec = dgSpec
                .Projects
                .FirstOrDefault(p => _fullProjectPath.Equals(
                    p.RestoreMetadata.ProjectPath,
                    StringComparison.OrdinalIgnoreCase));

            // Set the output path, since shelling out to MSBuild does not set this.
            packageSpec.RestoreMetadata.OutputPath = BaseIntermediateOutputPath;

            // Update the list of references while we have the package spec.
            lock (_installedPackagesLock)
            {
                _installedPackages = GetPackageReferences(packageSpec);
            }

            return packageSpec;
        }

        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            await Task.Yield();

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

                var packageSpec = GetPackageSpecForRestore();

                _installedPackages = GetPackageReferences(packageSpec);

                return _installedPackages;
            }
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

        private static class MsBuildUtility
        {
            private const string NuGetTargets = @"NuGet.PackageManagement.VisualStudio.NuGet.targets";

            public static DependencyGraphSpec GetProjectReferences(
                string msBuildDirectory,
                string[] projectPaths,
                int timeOut)
            {
                var msBuildPath = Path.Combine(msBuildDirectory, "msbuild.exe");

                if (!File.Exists(msBuildPath))
                {
                    throw new InvalidOperationException("msbuild.exe could not be found.");
                }

                var buildTasksDirectory = Path.GetDirectoryName(typeof(MsBuildUtility).Assembly.Location);
                var buildTasksPath = Path.Combine(buildTasksDirectory, "NuGet.Build.Tasks.dll");
                
                if (!File.Exists(buildTasksPath))
                {
                    throw new InvalidOperationException("NuGet.Build.Tasks.dll could not be found.");
                }

                using (var entryPointTargetPath = new TempFile(".targets"))
                using (var resultsPath = new TempFile(".result"))
                {
                    ExtractResource(NuGetTargets, entryPointTargetPath);

                    var argumentBuilder = new StringBuilder(
                        "/t:GenerateRestoreGraphFile " +
                        "/nologo /nr:false /v:q " +
                        "/p:BuildProjectReferences=false");

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
                        FileName = msBuildPath,
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
                using (var input = typeof(MsBuildUtility).Assembly.GetManifestResourceStream(resourceName))
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

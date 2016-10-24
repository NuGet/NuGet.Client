// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Implementation of the <see cref="IVsSolutionRestoreService"/>.
    /// Provides extension API for project restore nomination triggered by 3rd party component.
    /// Configured as a single-instance MEF part.
    /// </summary>
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IVsSolutionRestoreService))]
    internal sealed class VsSolutionRestoreService : IVsSolutionRestoreService
    {
        private const string IncludeAssets = "IncludeAssets";
        private const string ExcludeAssets = "ExcludeAssets";
        private const string PrivateAssets = "PrivateAssets";

        private readonly EnvDTE.DTE _dte;
        private readonly IProjectSystemCache _projectSystemCache;
        private readonly ISolutionRestoreWorker _restoreWorker;

        [ImportingConstructor]
        public VsSolutionRestoreService(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IProjectSystemCache projectSystemCache,
            ISolutionRestoreWorker restoreWorker)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (projectSystemCache == null)
            {
                throw new ArgumentNullException(nameof(projectSystemCache));
            }

            if (restoreWorker == null)
            {
                throw new ArgumentNullException(nameof(restoreWorker));
            }

            _dte = serviceProvider.GetDTE();
            _projectSystemCache = projectSystemCache;
            _restoreWorker = restoreWorker;
        }

        public Task<bool> CurrentRestoreOperation => _restoreWorker.CurrentRestoreOperation;

        public async Task<bool> NominateProjectAsync(string projectUniqueName, IVsProjectRestoreInfo projectRestoreInfo, CancellationToken token)
        {
            if (string.IsNullOrEmpty(projectUniqueName))
            {
                throw new ArgumentException(ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(projectUniqueName));
            }

            if (projectRestoreInfo == null)
            {
                throw new ArgumentNullException(nameof(projectRestoreInfo));
            }

            if (projectRestoreInfo.TargetFrameworks == null)
            {
                throw new InvalidOperationException("TargetFrameworks cannot be null.");
            }

            try
            {
                ActivityLog.LogInformation(
                    ExceptionHelper.LogEntrySource,
                    $"The nominate API is called for '{projectUniqueName}'.");

                var projectNames = await FindMatchingDteProjectAsync(projectUniqueName);
                if (projectNames != null)
                {
                    var packageSpec = ToPackageSpec(projectNames, projectRestoreInfo);
#if DEBUG
                    DumpProjectRestoreInfo(packageSpec);
#endif
                    _projectSystemCache.AddProjectRestoreInfo(projectNames, packageSpec);

                    // returned task completes when scheduled restore operation completes.
                    // it should be discarded as we don't want to block CPS on that.
                    var ignored = _restoreWorker.ScheduleRestoreAsync(
                        SolutionRestoreRequest.OnUpdate(),
                        token);

                    return true;
                }
                else
                {
                    ActivityLog.LogError(
                        ExceptionHelper.LogEntrySource,
                        $"Nominated project '{projectUniqueName}' cannot be found in DTE");
                }
            }
            catch (Exception e)
            {
                ExceptionHelper.WriteToActivityLog(e);
                throw;
            }

            return false;
        }

        // Try matching the nominated project to a DTE counterpart
        private async Task<ProjectNames> FindMatchingDteProjectAsync(string projectUniqueName)
        {
            var projectNames = await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    var dteProject = await TryGetDteProjectAsync(projectUniqueName);
                    if (dteProject != null)
                    {
                        // Get information about the project from DTE.
                        // TODO: Get rid off all DTE calls in this method
                        // TODO: cache should be indexed by full project path only.
                        // NuGet/Home#3729
                        return new ProjectNames(
                            fullName: projectUniqueName, // dteProject.FullName throws here
                            uniqueName: EnvDTEProjectUtility.GetUniqueName(dteProject),
                            shortName: EnvDTEProjectUtility.GetName(dteProject),
                            customUniqueName: EnvDTEProjectUtility.GetCustomUniqueName(dteProject));
                    }
                }
                catch (Exception ex)
                {
                    ExceptionHelper.WriteToActivityLog(ex);
                }

                return null;
            });

            return projectNames;
        }

        private async Task<EnvDTE.Project> TryGetDteProjectAsync(String projectUniqueName)
        {
            EnvDTE.Project dteProject = null;
            if (_projectSystemCache.TryGetDTEProject(projectUniqueName, out dteProject))
            {
                return dteProject;
            }

            var lookup = await EnvDTESolutionUtility.GetPathToDTEProjectLookupAsync(_dte);
            if (lookup.ContainsKey(projectUniqueName))
            {
                dteProject = lookup[projectUniqueName];
            }

            return dteProject;
        }

        private static void DumpProjectRestoreInfo(PackageSpec packageSpec)
        {
            try
            {
                var outputPath = packageSpec.RestoreMetadata.OutputPath;
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // Create dg file
                var dgFile = new DependencyGraphSpec();
                dgFile.AddRestore(packageSpec.RestoreMetadata.ProjectName);
                dgFile.AddProject(packageSpec);

                var dgPath = Path.Combine(outputPath, $"{Guid.NewGuid()}.dg");
                dgFile.Save(dgPath);
            }
            catch(Exception ex)
            {
                ExceptionHelper.WriteToActivityLog(ex);
            }
        }

        private static PackageSpec ToPackageSpec(ProjectNames projectNames, IVsProjectRestoreInfo projectRestoreInfo)
        {
            var tfis = projectRestoreInfo.TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .Select(ToTargetFrameworkInformation)
                .ToArray();

            var projectFullPath = projectNames.FullName;
            var projectDirectory = Path.GetDirectoryName(projectFullPath);

            var packageSpec = new PackageSpec(tfis)
            {
                Name = projectNames.ShortName,
                FilePath = projectFullPath,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectName = projectNames.ShortName,
                    ProjectUniqueName = projectNames.UniqueName,
                    ProjectPath = projectFullPath,
                    OutputPath = Path.GetFullPath(
                        Path.Combine(
                            projectDirectory,
                            projectRestoreInfo.BaseIntermediatePath)),
                    OutputType = RestoreOutputType.NETCore,
                    OriginalTargetFrameworks = tfis
                        .Select(tfi => tfi.FrameworkName.GetShortFolderName())
                        .ToList()
                }
            };

            return packageSpec;
        }

        private static TargetFrameworkInformation ToTargetFrameworkInformation(
            IVsTargetFrameworkInfo targetFrameworkInfo)
        {
            var tfi = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse(targetFrameworkInfo.TargetFrameworkMoniker)
            };

            if (targetFrameworkInfo.PackageReferences != null)
            {
                tfi.Dependencies.AddRange(
                    targetFrameworkInfo.PackageReferences
                        .Cast<IVsReferenceItem>()
                        .Select(ToPackageLibraryDependency));
            }

            if (targetFrameworkInfo.ProjectReferences != null)
            {
                tfi.Dependencies.AddRange(
                    targetFrameworkInfo.ProjectReferences
                        .Cast<IVsReferenceItem>()
                        .Select(ToProjectLibraryDependency));
            }

            return tfi;
        }

        private static PackageReference[] GetPackageReferences(
            IVsProjectRestoreInfo projectRestoreInfo)
        {
            var packageReferences = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .GroupBy(
                    keySelector: tfm =>
                        NuGetFramework.Parse(tfm.TargetFrameworkMoniker),
                    resultSelector: (key, tfms) =>
                        tfms.SelectMany(tfm =>
                            tfm.PackageReferences
                                .Cast<IVsReferenceItem>()
                                .Select(p => ToPackageReference(p, key))))
                .SelectMany(l => l)
                .ToList();

            var frameworkSorter = new NuGetFrameworkSorter();
            return packageReferences
                .GroupBy(
                    keySelector: p => p.PackageIdentity,
                    resultSelector: (id, ps) =>
                        ps.OrderBy(p => p.TargetFramework, frameworkSorter).First())
                .ToArray();
        }

        private static ProjectRestoreReference[] GetProjectReferences(
            IVsProjectRestoreInfo projectRestoreInfo)
        {
            var projectReferences = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .SelectMany(tfm =>
                    tfm.ProjectReferences
                        .Cast<IVsReferenceItem>()
                        .Select(ToProjectRestoreReference))
                .ToList();

            return projectReferences
                .GroupBy(
                    keySelector: p => p.ProjectPath,
                    resultSelector: (id, ps) => ps.First(),
                    comparer: StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static LibraryDependency ToPackageLibraryDependency(IVsReferenceItem item)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: item.Name,
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Package)
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                includeAssets: GetPropertyValueOrDefault(item, IncludeAssets),
                excludeAssets: GetPropertyValueOrDefault(item, ExcludeAssets),
                privateAssets: GetPropertyValueOrDefault(item, PrivateAssets));

            return dependency;
        }

        private static LibraryDependency ToProjectLibraryDependency(IVsReferenceItem item)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: item.Name,
                    versionRange: VersionRange.All,
                    typeConstraint: LibraryDependencyTarget.ExternalProject)
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                includeAssets: GetPropertyValueOrDefault(item, IncludeAssets),
                excludeAssets: GetPropertyValueOrDefault(item, ExcludeAssets),
                privateAssets: GetPropertyValueOrDefault(item, PrivateAssets));

            return dependency;
        }

        private static VersionRange GetVersionRange(IVsReferenceItem item)
        {
            var versionRange = GetPropertyValueOrDefault(item, "Version");

            if (!string.IsNullOrEmpty(versionRange))
            {
                return VersionRange.Parse(versionRange);
            }

            return VersionRange.All;
        }

        private static PackageReference ToPackageReference(
            IVsReferenceItem item, NuGetFramework framework)
        {
            var versionRange = GetVersionRange(item);
            var packageId = new PackageIdentity(item.Name, versionRange.MinVersion);
            var packageReference = new PackageReference(packageId, framework);

            return packageReference;
        }

        private static ProjectRestoreReference ToProjectRestoreReference(
            IVsReferenceItem item)
        {
            var projectPath = GetPropertyValueOrDefault(item, "ProjectFileFullPath");
            return new ProjectRestoreReference
            {
                ProjectUniqueName = item.Name,
                ProjectPath = projectPath
            };
        }

        private static string GetPropertyValueOrDefault(
            IVsReferenceItem item, string propertyName, string defaultValue = "")
        {
            try
            {
                IVsReferenceProperty property = item.Properties?.Item(propertyName);
                return property?.Value ?? defaultValue;
            }
            catch (ArgumentException)
            {
            }

            return defaultValue;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Build.Tasks.Pack
{
    /// <summary>
    /// Gets a list of project references from the assets file
    /// This list is then later traversed to determine the version
    /// of the project reference during pack.
    /// </summary>
    public class GetProjectReferencesFromAssetsFileTask : Microsoft.Build.Utilities.Task
    {
        public string RestoreOutputAbsolutePath { get; set; }

        public string ProjectAssetsFileAbsolutePath { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] ProjectReferences { get; set; }

        /// <summary>
        /// Project references that should be packed into the parent package.
        /// </summary>
        [Output]
        public ITaskItem[] PackedProjectReferences { get; set; }

        public override bool Execute()
        {
            var assetsFilePath = string.Empty;
            if (!string.IsNullOrEmpty(ProjectAssetsFileAbsolutePath) && File.Exists(ProjectAssetsFileAbsolutePath))
            {
                assetsFilePath = ProjectAssetsFileAbsolutePath;
            }
            else
            {
                assetsFilePath = Path.Combine(RestoreOutputAbsolutePath, LockFileFormat.AssetsFileName);
            }

            if (!File.Exists(assetsFilePath))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.AssetsFileNotFound,
                    assetsFilePath));
            }
            // The assets file is necessary for project and package references. Pack should not do any traversal,
            // so we leave that work up to restore (which produces the assets file).
            var lockFileFormat = new LockFileFormat();
            var assetsFile = lockFileFormat.Read(assetsFilePath);

            if (assetsFile.PackageSpec == null)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.AssetsFileDoesNotHaveValidPackageSpec,
                    assetsFilePath));
            }

            var projectDirectory = Path.GetDirectoryName(assetsFile.PackageSpec.RestoreMetadata.ProjectPath);
            // Using the libraries section of the assets file, the library name and version for the project path.
            var projectPathToLibraryIdentities = assetsFile
                .Libraries
                .Where(library => library.MSBuildProject != null)
                .Select(library => new TaskItem(Path.GetFullPath(Path.Combine(
                        projectDirectory,
                        PathUtility.GetPathWithDirectorySeparator(library.MSBuildProject)))));
            if (projectPathToLibraryIdentities != null)
            {
                ProjectReferences = projectPathToLibraryIdentities.ToArray();
            }
            else
            {
                ProjectReferences = Array.Empty<ITaskItem>();
            }

            PackedProjectReferences = CreatePackedProjectReferenceItems(assetsFile, projectDirectory).ToArray();

            return true;
        }

        private static IEnumerable<ITaskItem> CreatePackedProjectReferenceItems(LockFile assetsFile, string projectDirectory)
        {
            var projectLibraryPaths = assetsFile
                .Libraries
                .Where(library => library.MSBuildProject != null)
                .ToDictionary(
                    library => $"{library.Name}/{library.Version}",
                    library => Path.GetFullPath(Path.Combine(
                        projectDirectory,
                        PathUtility.GetPathWithDirectorySeparator(library.MSBuildProject))),
                    StringComparer.OrdinalIgnoreCase);

            var seen = new HashSet<string>(PathUtility.GetStringComparerBasedOnOS());

            foreach (var framework in assetsFile.PackageSpec.RestoreMetadata.TargetFrameworks)
            {
                var target = assetsFile.GetTarget(framework.TargetAlias, runtimeIdentifier: null);
                if (target == null)
                {
                    continue;
                }

                foreach (var projectReference in framework.ProjectReferences.Where(projectReference => projectReference.Pack))
                {
                    var projectLibrary = target.Libraries.FirstOrDefault(library =>
                        string.Equals(library.Type, LibraryType.Project, StringComparison.OrdinalIgnoreCase)
                        && projectLibraryPaths.TryGetValue($"{library.Name}/{library.Version}", out var projectPath)
                        && PathUtility.GetStringComparerBasedOnOS().Equals(projectPath, projectReference.ProjectPath));

                    foreach (var taskItem in CreatePackedProjectReferenceItems(
                        framework,
                        target.Libraries,
                        projectLibraryPaths,
                        projectReference.ProjectPath,
                        projectReference.PackagePath,
                        projectLibrary,
                        seen))
                    {
                        yield return taskItem;
                    }
                }
            }
        }

        private static IEnumerable<ITaskItem> CreatePackedProjectReferenceItems(
            ProjectRestoreMetadataFrameworkInfo framework,
            IEnumerable<LockFileTargetLibrary> targetLibraries,
            IReadOnlyDictionary<string, string> projectLibraryPaths,
            string projectPath,
            string packagePath,
            LockFileTargetLibrary projectLibrary,
            ISet<string> seen)
        {
            var targetFramework = string.IsNullOrEmpty(framework.TargetAlias)
                ? framework.FrameworkName.GetShortFolderName()
                : framework.TargetAlias;

            var key = $"{targetFramework}|{projectPath}";
            if (seen.Add(key))
            {
                yield return CreatePackedProjectReferenceItem(projectPath, targetFramework, packagePath);
            }

            if (projectLibrary == null)
            {
                yield break;
            }

            foreach (var dependency in projectLibrary.Dependencies)
            {
                var childLibrary = targetLibraries.FirstOrDefault(library =>
                    string.Equals(library.Type, LibraryType.Project, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(library.Name, dependency.Id, StringComparison.OrdinalIgnoreCase)
                    && dependency.VersionRange.Satisfies(library.Version));

                if (childLibrary == null
                    || !projectLibraryPaths.TryGetValue($"{childLibrary.Name}/{childLibrary.Version}", out var childProjectPath))
                {
                    continue;
                }

                foreach (var taskItem in CreatePackedProjectReferenceItems(
                    framework,
                    targetLibraries,
                    projectLibraryPaths,
                    childProjectPath,
                    packagePath,
                    childLibrary,
                    seen))
                {
                    yield return taskItem;
                }
            }
        }

        private static ITaskItem CreatePackedProjectReferenceItem(string projectPath, string targetFramework, string packagePath)
        {
            var taskItem = new TaskItem(projectPath);
            taskItem.SetMetadata("TargetFramework", targetFramework);
            taskItem.SetMetadata("AdditionalProperties", $"TargetFramework={targetFramework};BuildProjectReferences=false");

            if (!string.IsNullOrEmpty(packagePath))
            {
                taskItem.SetMetadata("PackagePath", packagePath);
                taskItem.SetMetadata("AdditionalProperties", $"TargetFramework={targetFramework};BuildProjectReferences=false;PackProjectReferencePackagePath={packagePath}");
            }

            return taskItem;
        }
    }
}

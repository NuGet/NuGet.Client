// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands.Utility;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.Utility
{
    public class PackagesConfigLockFileUtility
    {
        internal static async Task UpdateLockFileAsync(
            MSBuildNuGetProject msbuildProject,
            List<NuGetProjectAction> actionsList,
            List<SourceRepository> localRepositories,
            ProjectContextLogger logger,
            CancellationToken token)
        {
            var lockFileName = GetPackagesLockFilePath(msbuildProject);
            var lockFileExists = File.Exists(lockFileName);
            var enableLockFile = IsRestorePackagesWithLockFileEnabled(msbuildProject);
            if (enableLockFile == false && lockFileExists)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidLockFileInput, lockFileName);
                throw new InvalidOperationException(message);
            }
            else if (enableLockFile == true || lockFileExists)
            {
                var lockFile = GetLockFile(lockFileExists, lockFileName);
                var contentHashUtil = new ContentHashUtility(localRepositories, logger);
                await ApplyChangesAsync(lockFile, actionsList, contentHashUtil, token);
                PackagesLockFileFormat.Write(lockFileName, lockFile);

                // Add lock file to msbuild project, so it appears in solution explorer and is added to TFS source control.
                if (msbuildProject != null)
                {
                    var projectUri = new Uri(msbuildProject.MSBuildProjectPath);
                    var lockFileUri = new Uri(lockFileName);
                    var lockFileRelativePath = projectUri.MakeRelativeUri(lockFileUri).OriginalString;
                    if (Path.DirectorySeparatorChar != '/')
                    {
                        lockFileRelativePath.Replace('/', Path.DirectorySeparatorChar);
                    }
                    msbuildProject.ProjectSystem.AddExistingFile(lockFileRelativePath);
                }
            }
        }

        internal static string GetPackagesLockFilePath(MSBuildNuGetProject msbuildProject)
        {
            var directory = Path.GetDirectoryName(msbuildProject.MSBuildProjectPath);
            var msbuildProperty = msbuildProject?.ProjectSystem?.GetPropertyValue("NuGetLockFilePath");
            var projectName = Path.GetFileNameWithoutExtension(msbuildProject.MSBuildProjectPath);

            return GetPackagesLockFilePath(directory, msbuildProperty, projectName);
        }

        public static string GetPackagesLockFilePath(string projectPath, string nuGetLockFilePath, string projectName)
        {
            if (!string.IsNullOrWhiteSpace(nuGetLockFilePath))
            {
                return Path.Combine(projectPath, nuGetLockFilePath);
            }

            return PackagesLockFileUtilities.GetNuGetLockFilePath(projectPath, projectName);
        }

        private static bool? IsRestorePackagesWithLockFileEnabled(MSBuildNuGetProject msbuildProject)
        {
            var msbuildProperty = msbuildProject?.ProjectSystem?.GetPropertyValue("RestorePackagesWithLockFile");
            if (msbuildProperty is string restorePackagesWithLockFileValue)
            {
                if (bool.TryParse(restorePackagesWithLockFileValue, out var useLockFile))
                {
                    return useLockFile;
                }
            }

            return null;
        }

        internal static PackagesLockFile GetLockFile(bool lockFileExists, string lockFileName)
        {
            PackagesLockFile lockFile;

            if (lockFileExists)
            {
                lockFile = PackagesLockFileFormat.Read(lockFileName);
            }
            else
            {
                lockFile = new PackagesLockFile();
                lockFile.Targets.Add(new PackagesLockFileTarget
                {
                    TargetFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any)
                });
            }

            return lockFile;
        }

        internal static async Task ApplyChangesAsync(
            PackagesLockFile lockFile,
            List<NuGetProjectAction> actionsList,
            IContentHashUtility contentHashUtil,
            CancellationToken token)
        {
            RemoveUninstalledPackages(lockFile,
                actionsList.Where(a => a.NuGetProjectActionType == NuGetProjectActionType.Uninstall));
            await AddInstalledPackagesAsync(lockFile,
                actionsList.Where(a => a.NuGetProjectActionType == NuGetProjectActionType.Install),
                contentHashUtil,
                token);
        }

        private static void RemoveUninstalledPackages(PackagesLockFile lockFile, IEnumerable<NuGetProjectAction> actionsList)
        {
            foreach (var toUninstall in actionsList)
            {
                Debug.Assert(toUninstall.NuGetProjectActionType == NuGetProjectActionType.Uninstall);

                foreach (var installed in lockFile.Targets[0].Dependencies)
                {
                    if (string.Equals(installed.Id, toUninstall.PackageIdentity.Id, StringComparison.InvariantCultureIgnoreCase))
                    {
                        lockFile.Targets[0].Dependencies.Remove(installed);
                        return;
                    }
                }
            }
        }

        private static async Task AddInstalledPackagesAsync(
            PackagesLockFile lockFile,
            IEnumerable<NuGetProjectAction> actionsList,
            IContentHashUtility contentHashUtil,
            CancellationToken token)
        {
            foreach (var toInstall in actionsList)
            {
                Debug.Assert(toInstall.NuGetProjectActionType == NuGetProjectActionType.Install);

                var newDependency = new LockFileDependency
                {
                    Id = toInstall.PackageIdentity.Id,
                    ContentHash = await contentHashUtil.GetContentHashAsync(toInstall.PackageIdentity, token),
                    RequestedVersion = new VersionRange(toInstall.PackageIdentity.Version, true, toInstall.PackageIdentity.Version, true),
                    ResolvedVersion = toInstall.PackageIdentity.Version,
                    Type = PackageDependencyType.Direct
                };

                // should keep sorted, but lockFile[Targets[0].Dependencies is an IList<T>, but only List<T> has the .Sort() method, so we have to insert in sorted order.
                var index = 0;
                for (; index < lockFile.Targets[0].Dependencies.Count && string.Compare(lockFile.Targets[0].Dependencies[index].Id, toInstall.PackageIdentity.Id) < 0; index++) ;
                lockFile.Targets[0].Dependencies.Insert(index, newDependency);
            }
        }
    }
}

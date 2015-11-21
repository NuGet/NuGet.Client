// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.ProjectManagement
{
    internal static class MSBuildNuGetProjectSystemUtility
    {
        internal static FrameworkSpecificGroup GetMostCompatibleGroup(NuGetFramework projectTargetFramework,
            IEnumerable<FrameworkSpecificGroup> itemGroups)
        {
            var reducer = new FrameworkReducer();
            var mostCompatibleFramework
                = reducer.GetNearest(projectTargetFramework, itemGroups.Select(i => i.TargetFramework));
            if (mostCompatibleFramework != null)
            {
                var mostCompatibleGroup
                    = itemGroups.FirstOrDefault(i => i.TargetFramework.Equals(mostCompatibleFramework));

                if (IsValid(mostCompatibleGroup))
                {
                    return mostCompatibleGroup;
                }
            }

            return null;
        }

        /// <summary>
        /// Filter out invalid package items and replace the directory separator with the correct slash for the 
        /// current OS.
        /// </summary>
        /// <remarks>If the group is null or contains only only _._ this method will return the same group.</remarks>
        internal static FrameworkSpecificGroup Normalize(FrameworkSpecificGroup group)
        {
            // Default to returning the same group
            var result = group;

            // If the group is null or it does not contain any items besides _._ then this is a no-op.
            // If it does have items create a new normalized group to replace it with.
            if (group?.Items.Any() == true)
            {
                // Filter out invalid files
                var normalizedItems = GetValidPackageItems(group.Items)
                                            .Select(item => PathUtility.ReplaceAltDirSeparatorWithDirSeparator(item));

                // Create a new group
                result = new FrameworkSpecificGroup(
                    targetFramework: group.TargetFramework,
                    items: normalizedItems);
            }

            return result;
        }

        internal static bool IsValid(FrameworkSpecificGroup frameworkSpecificGroup)
        {
            if (frameworkSpecificGroup != null)
            {
                return (frameworkSpecificGroup.HasEmptyFolder
                     || frameworkSpecificGroup.Items.Any()
                     || !frameworkSpecificGroup.TargetFramework.Equals(NuGetFramework.AnyFramework));
            }

            return false;
        }

        internal static void TryAddFile(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, Func<Stream> content)
        {
            if (msBuildNuGetProjectSystem.FileExistsInProject(path))
            {
                // file exists in project, ask user if he wants to overwrite or ignore
                var conflictMessage = string.Format(CultureInfo.CurrentCulture,
                    Strings.FileConflictMessage, path, msBuildNuGetProjectSystem.ProjectName);
                var fileConflictAction = msBuildNuGetProjectSystem.NuGetProjectContext.ResolveFileConflict(conflictMessage);
                if (fileConflictAction == FileConflictAction.Overwrite
                    || fileConflictAction == FileConflictAction.OverwriteAll)
                {
                    // overwrite
                    msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Info, Strings.Info_OverwritingExistingFile, path);
                    using (var stream = content())
                    {
                        msBuildNuGetProjectSystem.AddFile(path, stream);
                    }
                }
                else
                {
                    // ignore
                    msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Warning, Strings.Warning_FileAlreadyExists, path);
                }
            }
            else
            {
                msBuildNuGetProjectSystem.AddFile(path, content());
            }
        }

        internal static void AddFiles(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem,
            PackageReaderBase packageReader,
            FrameworkSpecificGroup frameworkSpecificGroup,
            IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers)
        {
            var packageTargetFramework = frameworkSpecificGroup.TargetFramework;

            var packageItemListAsArchiveEntryNames = frameworkSpecificGroup.Items.ToList();
            packageItemListAsArchiveEntryNames.Sort(new PackageItemComparer());
            try
            {
                try
                {
                    var paths = packageItemListAsArchiveEntryNames.Select(file => ResolvePath(fileTransformers, fte => fte.InstallExtension,
                        GetEffectivePathForContentFile(packageTargetFramework, file)));
                    paths = paths.Where(p => !string.IsNullOrEmpty(p));

                    msBuildNuGetProjectSystem.BeginProcessing();
                    msBuildNuGetProjectSystem.RegisterProcessedFiles(paths);
                }
                catch (Exception)
                {
                    // Ignore all exceptions for now
                }

                foreach (var file in packageItemListAsArchiveEntryNames)
                {
                    if (IsEmptyFolder(file))
                    {
                        continue;
                    }

                    var effectivePathForContentFile = GetEffectivePathForContentFile(packageTargetFramework, file);

                    // Resolve the target path
                    IPackageFileTransformer installTransformer;
                    var path = ResolveTargetPath(msBuildNuGetProjectSystem,
                        fileTransformers,
                        fte => fte.InstallExtension, effectivePathForContentFile, out installTransformer);

                    if (msBuildNuGetProjectSystem.IsSupportedFile(path))
                    {
                        if (installTransformer != null)
                        {
                            installTransformer.TransformFile(() => packageReader.GetStream(file), path, msBuildNuGetProjectSystem);
                        }
                        else
                        {
                            // Ignore uninstall transform file during installation
                            string truncatedPath;
                            var uninstallTransformer =
                                FindFileTransformer(fileTransformers, fte => fte.UninstallExtension, effectivePathForContentFile, out truncatedPath);
                            if (uninstallTransformer != null)
                            {
                                continue;
                            }
                            TryAddFile(msBuildNuGetProjectSystem, path, () => packageReader.GetStream(file));
                        }
                    }
                }
            }
            finally
            {
                msBuildNuGetProjectSystem.EndProcessing();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        internal static void DeleteFiles(IMSBuildNuGetProjectSystem projectSystem,
            ZipArchive zipArchive,
            IEnumerable<string> otherPackagesPath,
            FrameworkSpecificGroup frameworkSpecificGroup,
            IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers)
        {
            var packageTargetFramework = frameworkSpecificGroup.TargetFramework;
            IPackageFileTransformer transformer;

            try
            {
                projectSystem.BeginProcessing();


                var directoryLookup = frameworkSpecificGroup.Items.ToLookup(
                    p => Path.GetDirectoryName(ResolveTargetPath(projectSystem,
                        fileTransformers,
                        fte => fte.UninstallExtension,
                        GetEffectivePathForContentFile(packageTargetFramework, p),
                        out transformer)));

                // Get all directories that this package may have added
                var directories = from grouping in directoryLookup
                                  from directory in FileSystemUtility.GetDirectories(grouping.Key, altDirectorySeparator: false)
                                  orderby directory.Length descending
                                  select directory;

                string projectFullPath = projectSystem.ProjectFullPath;

                // Remove files from every directory
                foreach (var directory in directories)
                {
                    var directoryFiles = directoryLookup.Contains(directory) ? directoryLookup[directory] : Enumerable.Empty<string>();

                    if (!Directory.Exists(Path.Combine(projectFullPath, directory)))
                    {
                        continue;
                    }

                    foreach (var file in directoryFiles)
                    {
                        if (IsEmptyFolder(file))
                        {
                            continue;
                        }

                        // Resolve the path
                        var path = ResolveTargetPath(projectSystem,
                            fileTransformers,
                            fte => fte.UninstallExtension,
                            GetEffectivePathForContentFile(packageTargetFramework, file),
                            out transformer);

                        if (projectSystem.IsSupportedFile(path))
                        {
                            // Register the file being uninstalled (used by web site project system).
                            projectSystem.RegisterProcessedFiles(new[] { path });

                            if (transformer != null)
                            {
                                // TODO: use the framework from packages.config instead of the current framework
                                // which may have changed during re-targeting
                                var projectFramework = projectSystem.TargetFramework;

                                var matchingFiles = new List<InternalZipFileInfo>();
                                foreach (var otherPackagePath in otherPackagesPath)
                                {
                                    using (var otherPackageStream = File.OpenRead(otherPackagePath))
                                    {
                                        var otherPackageZipArchive = new ZipArchive(otherPackageStream);
                                        var otherPackageZipReader = new PackageReader(otherPackageZipArchive);

                                        // use the project framework to find the group that would have been installed
                                        var mostCompatibleContentFilesGroup = GetMostCompatibleGroup(
                                            projectFramework,
                                            otherPackageZipReader.GetContentItems());

                                        if (IsValid(mostCompatibleContentFilesGroup))
                                        {
                                            // Should not normalize content files group.
                                            // It should be like a ZipFileEntry with a forward slash.
                                            foreach (var otherPackageItem in mostCompatibleContentFilesGroup.Items)
                                            {
                                                if (GetEffectivePathForContentFile(packageTargetFramework, otherPackageItem)
                                                    .Equals(GetEffectivePathForContentFile(packageTargetFramework, file), StringComparison.OrdinalIgnoreCase))
                                                {
                                                    matchingFiles.Add(new InternalZipFileInfo(otherPackagePath, otherPackageItem));
                                                }
                                            }
                                        }
                                    }
                                }

                                try
                                {
                                    var zipArchiveFileEntry = PathUtility.GetEntry(zipArchive, file);
                                    if (zipArchiveFileEntry != null)
                                    {
                                        transformer.RevertFile(zipArchiveFileEntry.Open, path, matchingFiles, projectSystem);
                                    }
                                }
                                catch (Exception e)
                                {
                                    projectSystem.NuGetProjectContext.Log(MessageLevel.Warning, e.Message);
                                }
                            }
                            else
                            {
                                var zipArchiveFileEntry = PathUtility.GetEntry(zipArchive, file);
                                if (zipArchiveFileEntry != null)
                                {
                                    DeleteFileSafe(path, zipArchiveFileEntry.Open, projectSystem);
                                }
                            }
                        }
                    }

                    // If the directory is empty then delete it
                    if (!GetFilesSafe(projectSystem, directory).Any()
                        && !GetDirectoriesSafe(projectSystem, directory).Any())
                    {
                        DeleteDirectorySafe(projectSystem, directory);
                    }
                }
            }
            finally
            {
                projectSystem.EndProcessing();
            }
        }

        internal static IEnumerable<string> GetFilesSafe(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path)
        {
            return GetFilesSafe(msBuildNuGetProjectSystem, path, "*.*");
        }

        internal static IEnumerable<string> GetFilesSafe(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, string filter)
        {
            try
            {
                return GetFiles(msBuildNuGetProjectSystem, path, filter, recursive: false);
            }
            catch (Exception e)
            {
                msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Warning, e.Message);
            }

            return Enumerable.Empty<string>();
        }

        internal static IEnumerable<string> GetFiles(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, string filter, bool recursive)
        {
            return msBuildNuGetProjectSystem.GetFiles(path, filter, recursive);
        }

        internal static void DeleteFileSafe(string path, Func<Stream> streamFactory, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            // Only delete the file if it exists and the checksum is the same
            if (msBuildNuGetProjectSystem.FileExistsInProject(path))
            {
                var fullPath = Path.Combine(msBuildNuGetProjectSystem.ProjectFullPath, path);
                if (FileSystemUtility.ContentEquals(fullPath, streamFactory))
                {
                    PerformSafeAction(() => msBuildNuGetProjectSystem.RemoveFile(path), msBuildNuGetProjectSystem.NuGetProjectContext);
                }
                else
                {
                    // This package installed a file that was modified so warn the user
                    msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Warning, Strings.Warning_FileModified, fullPath);
                }
            }
        }

        internal static IEnumerable<string> GetDirectoriesSafe(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path)
        {
            try
            {
                return GetDirectories(msBuildNuGetProjectSystem, path);
            }
            catch (Exception e)
            {
                msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Warning, e.Message);
            }

            return Enumerable.Empty<string>();
        }

        internal static IEnumerable<string> GetDirectories(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path)
        {
            return msBuildNuGetProjectSystem.GetDirectories(path);
        }

        internal static void DeleteDirectorySafe(IMSBuildNuGetProjectSystem projectSystem, string path)
        {
            PerformSafeAction(() => DeleteDirectory(projectSystem, path), projectSystem.NuGetProjectContext);
        }

        // Deletes an empty folder from disk and the project
        private static void DeleteDirectory(IMSBuildNuGetProjectSystem projectSystem, string path)
        {
            var fullPath = Path.Combine(projectSystem.ProjectFullPath, path);
            if (!Directory.Exists(fullPath))
            {
                return;
            }

            // Only delete this folder if it is empty and we didn't specify that we want to recurse
            if (GetFiles(projectSystem, path, "*.*", recursive: false).Any() || GetDirectories(projectSystem, path).Any())
            {
                projectSystem.NuGetProjectContext.Log(MessageLevel.Warning, Strings.Warning_DirectoryNotEmpty, path);
                return;
            }
            projectSystem.RegisterProcessedFiles(new[] { path });

            projectSystem.DeleteDirectory(path, recursive: false);

            // Workaround for update-package TFS issue. If we're bound to TFS, do not try and delete directories.
            var sourceControlManager = SourceControlUtility.GetSourceControlManager(projectSystem.NuGetProjectContext);
            if (sourceControlManager != null)
            {
                // Source control bound, do not delete
                return;
            }

            // For potential project systems that do not remove items from disk, we delete the folder directly
            // There is no actual scenario where we know this is broken without the code below, but since the
            // code was always there, we are leaving it behind for now.
            if (!Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: false);

                // The directory is not guaranteed to be gone since there could be
                // other open handles. Wait, up to half a second, until the directory is gone.
                for (var i = 0; Directory.Exists(fullPath) && i < 5; ++i)
                {
                    Thread.Sleep(100);
                }

                projectSystem.RegisterProcessedFiles(new[] { path });

                projectSystem.NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFolder, fullPath);
            }
        }

        private static void PerformSafeAction(Action action, INuGetProjectContext nuGetProjectContext)
        {
            try
            {
                Attempt(action);
            }
            catch (Exception e)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, e.Message);
            }
        }

        private static void Attempt(Action action, int retries = 3, int delayBeforeRetry = 150)
        {
            while (retries > 0)
            {
                try
                {
                    action();
                    break;
                }
                catch
                {
                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }
                Thread.Sleep(delayBeforeRetry);
            }
        }

        private static bool IsEmptyFolder(string packageFilePath)
        {
            return packageFilePath != null &&
                   PackagingCoreConstants.EmptyFolder.Equals(Path.GetFileName(packageFilePath), StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePath(
            IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers,
            Func<FileTransformExtensions, string> extensionSelector,
            string effectivePath)
        {
            string truncatedPath;

            // Remove the transformer extension (e.g. .pp, .transform)
            var transformer = FindFileTransformer(
                fileTransformers, extensionSelector, effectivePath, out truncatedPath);

            if (transformer != null)
            {
                effectivePath = truncatedPath;
            }

            return effectivePath;
        }

        private static string ResolveTargetPath(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem,
            IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers,
            Func<FileTransformExtensions, string> extensionSelector,
            string effectivePath,
            out IPackageFileTransformer transformer)
        {
            string truncatedPath;

            // Remove the transformer extension (e.g. .pp, .transform)
            transformer = FindFileTransformer(fileTransformers, extensionSelector, effectivePath, out truncatedPath);
            if (transformer != null)
            {
                effectivePath = truncatedPath;
            }

            return msBuildNuGetProjectSystem.ResolvePath(effectivePath);
        }

        private static IPackageFileTransformer FindFileTransformer(
            IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers,
            Func<FileTransformExtensions, string> extensionSelector,
            string effectivePath,
            out string truncatedPath)
        {
            foreach (var transformExtensions in fileTransformers.Keys)
            {
                var extension = extensionSelector(transformExtensions);
                if (effectivePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    truncatedPath = effectivePath.Substring(0, effectivePath.Length - extension.Length);

                    // Bug 1686: Don't allow transforming packages.config.transform,
                    // but we still want to copy packages.config.transform as-is into the project.
                    var fileName = Path.GetFileName(truncatedPath);
                    if (!Constants.PackageReferenceFile.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return fileTransformers[transformExtensions];
                    }
                }
            }

            truncatedPath = effectivePath;
            return null;
        }

        private static string GetEffectivePathForContentFile(NuGetFramework nuGetFramework, string zipArchiveEntryFullName)
        {
            // Always use Path.DirectorySeparatorChar
            var effectivePathForContentFile = PathUtility.ReplaceAltDirSeparatorWithDirSeparator(zipArchiveEntryFullName);

            if (effectivePathForContentFile.StartsWith(PackagingConstants.Folders.Content + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                effectivePathForContentFile = effectivePathForContentFile.Substring((PackagingConstants.Folders.Content + Path.DirectorySeparatorChar).Length);
                if (!nuGetFramework.Equals(NuGetFramework.AnyFramework))
                {
                    // Parsing out the framework name out of the effective path
                    var frameworkFolderEndIndex = effectivePathForContentFile.IndexOf(Path.DirectorySeparatorChar);
                    if (frameworkFolderEndIndex != -1)
                    {
                        if (effectivePathForContentFile.Length > frameworkFolderEndIndex + 1)
                        {
                            effectivePathForContentFile = effectivePathForContentFile.Substring(frameworkFolderEndIndex + 1);
                        }
                    }

                    return effectivePathForContentFile;
                }
            }

            // Return the effective path with Path.DirectorySeparatorChar
            return effectivePathForContentFile;
        }

        internal static IEnumerable<string> GetValidPackageItems(IEnumerable<string> items)
        {
            if (items == null
                || !items.Any())
            {
                return Enumerable.Empty<string>();
            }

            // Assume nupkg and nuspec as the save mode for identifying valid package files
            return items.Where(i => PackageHelper.IsPackageFile(i, PackageSaveModes.Nupkg | PackageSaveModes.Nuspec));
        }

        internal static void AddFile(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, Action<Stream> writeToStream)
        {
            using (var memoryStream = new MemoryStream())
            {
                writeToStream(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                msBuildNuGetProjectSystem.AddFile(path, memoryStream);
            }
        }

        private class PackageItemComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                // BUG 636: We sort files so that they are added in the correct order
                // e.g aspx before aspx.cs

                if (x.Equals(y, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                // Add files that are prefixes of other files first
                if (x.StartsWith(y, StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }

                if (y.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                {
                    return 1;
                }

                return string.Compare(y, x, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
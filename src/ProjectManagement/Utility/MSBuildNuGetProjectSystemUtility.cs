using NuGet.Frameworks;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    internal static class MSBuildNuGetProjectSystemUtility
    {
        internal static FrameworkSpecificGroup GetMostCompatibleGroup(NuGetFramework projectTargetFramework, IEnumerable<FrameworkSpecificGroup> itemGroups,
            bool altDirSeparator = false)
        {
            FrameworkReducer reducer = new FrameworkReducer();
            NuGetFramework mostCompatibleFramework = reducer.GetNearest(projectTargetFramework, itemGroups.Select(i => NuGetFramework.Parse(i.TargetFramework)));
            if (mostCompatibleFramework != null)
            {
                IEnumerable<FrameworkSpecificGroup> mostCompatibleGroups = itemGroups.Where(i => NuGetFramework.Parse(i.TargetFramework).Equals(mostCompatibleFramework));
                var mostCompatibleGroup = mostCompatibleGroups.FirstOrDefault();
                if (IsValid(mostCompatibleGroup))
                {
                    mostCompatibleGroup = new FrameworkSpecificGroup(mostCompatibleGroup.TargetFramework,
                        mostCompatibleGroup.Items.Select(item => altDirSeparator ? MSBuildNuGetProjectSystemUtility.ReplaceDirSeparatorWithAltDirSeparator(item)
                            : MSBuildNuGetProjectSystemUtility.ReplaceAltDirSeparatorWithDirSeparator(item)));
                }

                return mostCompatibleGroup;
            }
            return null;
        }

        internal static bool IsValid(FrameworkSpecificGroup frameworkSpecificGroup)
        {
            // It is possible for a compatible frameworkSpecificGroup, there are no items
            return (frameworkSpecificGroup != null && frameworkSpecificGroup.Items != null);
        }

        internal static void TryAddFile(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, Func<Stream> content)
        {
            if (msBuildNuGetProjectSystem.FileExistsInProject(path))
            {
                // file exists in project, ask user if he wants to overwrite or ignore
                string conflictMessage = String.Format(CultureInfo.CurrentCulture,
                    Strings.FileConflictMessage, path, msBuildNuGetProjectSystem.ProjectName);
                FileConflictAction fileConflictAction = msBuildNuGetProjectSystem.NuGetProjectContext.ResolveFileConflict(conflictMessage);
                if (fileConflictAction == FileConflictAction.Overwrite || fileConflictAction == FileConflictAction.OverwriteAll)
                {
                    // overwrite
                    msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Info, Strings.Info_OverwritingExistingFile, path);
                    using (Stream stream = content())
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
                                        ZipArchive zipArchive,
                                        FrameworkSpecificGroup frameworkSpecificGroup,
                                        IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers)
        {
            // Content files are maintained with AltDirectorySeparatorChar
            List<string> packageItemListAsArchiveEntryNames = frameworkSpecificGroup.Items.Select(i => ReplaceDirSeparatorWithAltDirSeparator(i)).ToList();

            packageItemListAsArchiveEntryNames.Sort(new PackageItemComparer());
            try
            {
                var zipArchiveEntryList = packageItemListAsArchiveEntryNames.Select(i => zipArchive.GetEntry(i)).ToList();
                foreach (ZipArchiveEntry zipArchiveEntry in zipArchiveEntryList)
                {
                    if (zipArchiveEntry == null)
                    {
                        throw new ArgumentNullException("zipArchiveEntry");
                    }

                    if (IsEmptyFolder(zipArchiveEntry.FullName))
                    {
                        continue;
                    }

                    var effectivePathForContentFile = GetEffectivePathForContentFile(msBuildNuGetProjectSystem.TargetFramework, zipArchiveEntry.FullName);

                    // Resolve the target path
                    IPackageFileTransformer installTransformer;
                    string path = ResolveTargetPath(msBuildNuGetProjectSystem,
                        fileTransformers,
                        fte => fte.InstallExtension, effectivePathForContentFile, out installTransformer);

                    if (msBuildNuGetProjectSystem.IsSupportedFile(path))
                    {
                        if (installTransformer != null)
                        {
                            installTransformer.TransformFile(zipArchiveEntry, path, msBuildNuGetProjectSystem);
                        }
                        else
                        {
                            // Ignore uninstall transform file during installation
                            string truncatedPath;
                            IPackageFileTransformer uninstallTransformer =
                                FindFileTransformer(fileTransformers, fte => fte.UninstallExtension, effectivePathForContentFile, out truncatedPath);
                            if (uninstallTransformer != null)
                            {
                                continue;
                            }
                            TryAddFile(msBuildNuGetProjectSystem, path, zipArchiveEntry.Open);
                        }
                    }
                }
            }
            finally
            {

            }
        }

        internal static void DeleteFiles(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem,
                                            ZipArchive zipArchive,
                                            IEnumerable<string> otherPackagesPath,
                                            FrameworkSpecificGroup frameworkSpecificGroup,
                                            IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers)
        {
            var packageTargetFramework = NuGetFramework.Parse(frameworkSpecificGroup.TargetFramework);
            IPackageFileTransformer transformer;
            var directoryLookup = frameworkSpecificGroup.Items.ToLookup(
                p => Path.GetDirectoryName(ResolveTargetPath(msBuildNuGetProjectSystem,
                    fileTransformers,
                    fte => fte.UninstallExtension,
                    GetEffectivePathForContentFile(packageTargetFramework, p),
                    out transformer)));

            // Get all directories that this package may have added
            var directories = from grouping in directoryLookup
                              from directory in GetDirectories(grouping.Key, altDirectorySeparator: false)
                              orderby directory.Length descending
                              select directory;

            // Remove files from every directory
            foreach (var directory in directories)
            {
                var directoryFiles = directoryLookup.Contains(directory) ? directoryLookup[directory] : Enumerable.Empty<string>();

                if (!Directory.Exists(Path.Combine(msBuildNuGetProjectSystem.ProjectFullPath, directory)))
                {
                    continue;
                }

                try
                {
                    foreach (var file in directoryFiles)
                    {
                        if (IsEmptyFolder(file))
                        {
                            continue;
                        }

                        // Resolve the path
                        string path = ResolveTargetPath(msBuildNuGetProjectSystem,
                                                        fileTransformers,
                                                        fte => fte.UninstallExtension,
                                                        GetEffectivePathForContentFile(packageTargetFramework, file),
                                                        out transformer);

                        if (msBuildNuGetProjectSystem.IsSupportedFile(path))
                        {
                            if (transformer != null)
                            {
                                List<InternalZipFileInfo> matchingFiles = new List<InternalZipFileInfo>();
                                foreach(var otherPackagePath in otherPackagesPath)
                                {
                                    using(var otherPackageStream = File.OpenRead(otherPackagePath))
                                    {
                                        var otherPackageZipArchive = new ZipArchive(otherPackageStream);
                                        var otherPackageZipReader = new PackageReader(otherPackageZipArchive);
                                        var mostCompatibleContentFilesGroup = GetMostCompatibleGroup(packageTargetFramework, otherPackageZipReader.GetContentItems(), altDirSeparator: true);
                                        if(IsValid(mostCompatibleContentFilesGroup))
                                        {
                                            foreach(var otherPackageItem in mostCompatibleContentFilesGroup.Items)
                                            {
                                                if(GetEffectivePathForContentFile(packageTargetFramework, otherPackageItem)
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
                                    var zipArchiveFileEntry = zipArchive.GetEntry(ReplaceDirSeparatorWithAltDirSeparator(file));
                                    transformer.RevertFile(zipArchiveFileEntry, path, matchingFiles, msBuildNuGetProjectSystem);
                                }
                                catch (Exception e)
                                {
                                    msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Warning, e.Message);
                                }
                            }
                            else
                            {
                                var zipArchiveFileEntry = zipArchive.GetEntry(ReplaceDirSeparatorWithAltDirSeparator(file));
                                DeleteFileSafe(path, zipArchiveFileEntry.Open, msBuildNuGetProjectSystem);
                            }
                        }
                    }


                    // If the directory is empty then delete it
                    if (!GetFilesSafe(msBuildNuGetProjectSystem, directory).Any() &&
                        !GetDirectoriesSafe(msBuildNuGetProjectSystem, directory).Any())
                    {
                        DeleteDirectorySafe(msBuildNuGetProjectSystem, directory, recursive: false);
                    }
                }
                finally
                {

                }
            }
        }

        public static IEnumerable<string> GetFilesSafe(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path)
        {
            return GetFilesSafe(msBuildNuGetProjectSystem, path, "*.*");
        }

        public static IEnumerable<string> GetFilesSafe(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, string filter)
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

        public static IEnumerable<string> GetFiles(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, string filter, bool recursive)
        {
            return FileSystemUtility.GetFiles(msBuildNuGetProjectSystem.ProjectFullPath, path, filter, recursive);
        }

        public static void DeleteFileSafe(string path, Func<Stream> streamFactory, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            // Only delete the file if it exists and the checksum is the same
            if (msBuildNuGetProjectSystem.FileExistsInProject(path))
            {
                var fullPath = Path.Combine(msBuildNuGetProjectSystem.ProjectFullPath, path);
                if (ContentEquals(fullPath, streamFactory))
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

        public static IEnumerable<string> GetDirectoriesSafe(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path)
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

        public static IEnumerable<string> GetDirectories(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path)
        {
            try
            {
                path = PathUtility.EnsureTrailingSlash(Path.Combine(msBuildNuGetProjectSystem.ProjectFullPath, path));
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateDirectories(path)
                                .Select(p => p.Substring(msBuildNuGetProjectSystem.ProjectFullPath.Length).TrimStart(Path.DirectorySeparatorChar));
            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (DirectoryNotFoundException)
            {

            }

            return Enumerable.Empty<string>();
        }

        private static bool ContentEquals(string path, Func<Stream> streamFactory)
        {
            using (Stream stream = streamFactory(),
                fileStream = File.OpenRead(path))
            {
                return stream.ContentEquals(fileStream);
            }
        }

        public static void DeleteDirectorySafe(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, bool recursive)
        {
            PerformSafeAction(() => DeleteDirectory(msBuildNuGetProjectSystem, path, recursive), msBuildNuGetProjectSystem.NuGetProjectContext);
        }

        public static void DeleteDirectory(IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, string path, bool recursive)
        {
            var fullPath = Path.Combine(msBuildNuGetProjectSystem.ProjectFullPath, path);
            if (!Directory.Exists(fullPath))
            {
                return;
            }

            try
            {
                Directory.Delete(fullPath, recursive);

                // The directory is not guaranteed to be gone since there could be
                // other open handles. Wait, up to half a second, until the directory is gone.
                for (int i = 0; Directory.Exists(fullPath) && i < 5; ++i)
                {
                    System.Threading.Thread.Sleep(100);
                }

                msBuildNuGetProjectSystem.NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFolder, fullPath);
            }
            catch (DirectoryNotFoundException)
            {
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

        internal static IEnumerable<string> GetDirectories(string path, bool altDirectorySeparator)
        {
            foreach (var index in IndexOfAll(path, altDirectorySeparator ? Path.AltDirectorySeparatorChar : Path.DirectorySeparatorChar))
            {
                yield return path.Substring(0, index);
            }
            yield return path;
        }

        private static IEnumerable<int> IndexOfAll(string value, char ch)
        {
            int index = -1;
            do
            {
                index = value.IndexOf(ch, index + 1);
                if (index >= 0)
                {
                    yield return index;
                }
            }
            while (index >= 0);
        }

        private static bool IsEmptyFolder(string packageFilePath)
        {
            return packageFilePath != null &&
                   Constants.PackageEmptyFileName.Equals(Path.GetFileName(packageFilePath), StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePath(
            IDictionary<FileTransformExtensions, IPackageFileTransformer> fileTransformers,
            Func<FileTransformExtensions, string> extensionSelector,
            string effectivePath)
        {

            string truncatedPath;

            // Remove the transformer extension (e.g. .pp, .transform)
            IPackageFileTransformer transformer = FindFileTransformer(
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
                string extension = extensionSelector(transformExtensions);
                if (effectivePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    truncatedPath = effectivePath.Substring(0, effectivePath.Length - extension.Length);

                    // Bug 1686: Don't allow transforming packages.config.transform,
                    // but we still want to copy packages.config.transform as-is into the project.
                    string fileName = Path.GetFileName(truncatedPath);
                    if (!Constants.PackageReferenceFile.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return fileTransformers[transformExtensions];
                    }
                }
            }

            truncatedPath = effectivePath;
            return null;
        }

        internal static string ReplaceAltDirSeparatorWithDirSeparator(string path)
        {
            return Uri.UnescapeDataString(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        }

        internal static string ReplaceDirSeparatorWithAltDirSeparator(string path)
        {
            return Uri.UnescapeDataString(path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        private static string GetEffectivePathForContentFile(NuGetFramework nuGetFramework, string zipArchiveEntryFullName)
        {
            // Always use Path.DirectorySeparatorChar
            var effectivePathForContentFile = ReplaceAltDirSeparatorWithDirSeparator(zipArchiveEntryFullName);

            if (effectivePathForContentFile.StartsWith(Constants.ContentDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                effectivePathForContentFile = effectivePathForContentFile.Substring((Constants.ContentDirectory + Path.DirectorySeparatorChar).Length);
                if(nuGetFramework.Equals(NuGetFramework.AnyFramework))
                {
                    //// TODO: Content files cannot be target framework specific has been made as an assumption
                    int frameworkFolderEndIndex = effectivePathForContentFile.IndexOf(Path.AltDirectorySeparatorChar);
                    if (frameworkFolderEndIndex != -1)
                    {
                        var potentialFrameworkName = effectivePathForContentFile.Substring(0, frameworkFolderEndIndex);
                        if(nuGetFramework != NuGetFramework.UnsupportedFramework && NuGetFramework.Parse(potentialFrameworkName).Equals(nuGetFramework))
                        {
                            throw new ArgumentException(Strings.ContentFilesShouldNotBeTargetFrameworkSpecific, effectivePathForContentFile);
                        }                        
                    }

                    return effectivePathForContentFile;
                }
            }

            // Return the effective path with Path.DirectorySeparatorChar
            return effectivePathForContentFile;
        }
    }
}

using NuGet.Frameworks;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public static class MSBuildNuGetProjectSystemUtility
    {
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
            // Convert package items to a archive entry name. That is, replace Path.DirectorySeparatorChar with Path.AltDirectorySeparatorChar
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
            var effectivePathForContentFile = zipArchiveEntryFullName;
            // NOTE that ZipArchiveEntries have '/' in them, which is, Path.AltDirectorySeparatorChar
            if (zipArchiveEntryFullName.StartsWith(Constants.ContentDirectory + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                effectivePathForContentFile = zipArchiveEntryFullName.Substring((Constants.ContentDirectory + Path.AltDirectorySeparatorChar).Length);
                if(nuGetFramework.Equals(NuGetFramework.AnyFramework))
                {                    
                    //// TODO: Content files cannot be target framework specific has been made as an assumption
                    int frameworkFolderEndIndex = effectivePathForContentFile.IndexOf(Path.AltDirectorySeparatorChar);
                    if (frameworkFolderEndIndex != -1)
                    {
                        var potentialFrameworkName = effectivePathForContentFile.Substring(0, frameworkFolderEndIndex);
                        if(nuGetFramework != NuGetFramework.UnsupportedFramework && NuGetFramework.Parse(potentialFrameworkName).Equals(nuGetFramework))
                        {
                            throw new ArgumentException(Strings.ContentFilesShouldNotBeTargetFrameworkSpecific, zipArchiveEntryFullName);
                        }                        
                    }

                    return effectivePathForContentFile;
                }
            }

            // Return the effective path with Path.DirectorySeparatorChar
            return ReplaceAltDirSeparatorWithDirSeparator(effectivePathForContentFile);
        }
    }
}

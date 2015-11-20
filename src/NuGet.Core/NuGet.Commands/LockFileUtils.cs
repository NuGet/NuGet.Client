using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;

namespace NuGet.Commands
{
    internal static class LockFileUtils
    {
        public static LockFileTargetLibrary CreateLockFileTargetLibrary(
            LockFileLibrary library,
            LocalPackageInfo package,
            RestoreTargetGraph targetGraph,
            VersionFolderPathResolver defaultPackagePathResolver,
            string correctedPackageName,
            LibraryIncludeFlags dependencyType)
        {
            return CreateLockFileTargetLibrary(
                library,
                package,
                targetGraph,
                defaultPackagePathResolver,
                correctedPackageName,
                dependencyType: dependencyType,
                targetFrameworkOverride: null);
        }

        public static LockFileTargetLibrary CreateLockFileTargetLibrary(
            LockFileLibrary library,
            LocalPackageInfo package,
            RestoreTargetGraph targetGraph,
            VersionFolderPathResolver defaultPackagePathResolver,
            string correctedPackageName,
            LibraryIncludeFlags dependencyType,
            NuGetFramework targetFrameworkOverride)
        {
            var lockFileLib = new LockFileTargetLibrary();

            var framework = targetFrameworkOverride ?? targetGraph.Framework;
            var runtimeIdentifier = targetGraph.RuntimeIdentifier;

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;

            IList<string> files;
            var contentItems = new ContentItemCollection();
            HashSet<string> referenceFilter = null;

            // If the previous LockFileLibrary was given, use that to find the file list. Otherwise read the nupkg.
            if (library == null)
            {
                using (var nupkgStream = File.OpenRead(package.ZipPath))
                {
                    var packageReader = new PackageReader(nupkgStream);
                    if (Path.DirectorySeparatorChar != '/')
                    {
                        files = packageReader
                            .GetFiles()
                            .Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                            .ToList();
                    }
                    else
                    {
                        files = packageReader
                            .GetFiles()
                            .ToList();
                    }
                }
            }
            else
            {
                if (Path.DirectorySeparatorChar != '/')
                {
                    files = library.Files.Select(p => p.Replace(Path.DirectorySeparatorChar, '/')).ToList();
                }
                else
                {
                    files = library.Files;
                }
            }

            contentItems.Load(files);

            NuspecReader nuspec = null;

            var nuspecPath = defaultPackagePathResolver.GetManifestFilePath(package.Id, package.Version);

            if (File.Exists(nuspecPath))
            {
                using (var stream = File.OpenRead(nuspecPath))
                {
                    nuspec = new NuspecReader(stream);
                }
            }
            else
            {
                var dir = defaultPackagePathResolver.GetPackageDirectory(package.Id, package.Version);
                var folderReader = new PackageFolderReader(dir);

                using (var stream = folderReader.GetNuspec())
                {
                    nuspec = new NuspecReader(stream);
                }
            }

            var dependencySet = nuspec
                .GetDependencyGroups()
                .GetNearest(framework);

            if (dependencySet != null)
            {
                var set = dependencySet.Packages;

                if (set != null)
                {
                    lockFileLib.Dependencies = set.ToList();
                }
            }

            var referenceSet = nuspec.GetReferenceGroups().GetNearest(framework);
            if (referenceSet != null)
            {
                referenceFilter = new HashSet<string>(referenceSet.Items, StringComparer.OrdinalIgnoreCase);
            }

            // TODO: Remove this when we do #596
            // ASP.NET Core isn't compatible with generic PCL profiles
            if (!string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.AspNetCore, StringComparison.OrdinalIgnoreCase)
                &&
                !string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.DnxCore, StringComparison.OrdinalIgnoreCase))
            {
                var frameworkAssemblies = nuspec.GetFrameworkReferenceGroups().GetNearest(framework);
                if (frameworkAssemblies != null)
                {
                    foreach (var assemblyReference in frameworkAssemblies.Items)
                    {
                        lockFileLib.FrameworkAssemblies.Add(assemblyReference);
                    }
                }
            }

            var managedCriteria = targetGraph.Conventions.Criteria.ForFrameworkAndRuntime(framework, targetGraph.RuntimeIdentifier);

            var compileGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.CompileAssemblies, targetGraph.Conventions.Patterns.RuntimeAssemblies);

            if (compileGroup != null)
            {
                lockFileLib.CompileTimeAssemblies = compileGroup.Items.Select(t => new LockFileItem(t.Path)).ToList();
            }

            var runtimeGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.RuntimeAssemblies);
            if (runtimeGroup != null)
            {
                lockFileLib.RuntimeAssemblies = runtimeGroup.Items.Select(p => new LockFileItem(p.Path)).ToList();
            }

            var resourceGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.ResourceAssemblies);
            if (resourceGroup != null)
            {
                lockFileLib.ResourceAssemblies = resourceGroup.Items.Select(ToResourceLockFileItem).ToList();
            }

            var nativeCriteria = targetGraph.Conventions.Criteria.ForRuntime(targetGraph.RuntimeIdentifier);

            var nativeGroup = contentItems.FindBestItemGroup(nativeCriteria, targetGraph.Conventions.Patterns.NativeLibraries);
            if (nativeGroup != null)
            {
                lockFileLib.NativeLibraries = nativeGroup.Items.Select(p => new LockFileItem(p.Path)).ToList();
            }

            // content v2 items
            var contentFileGroups = contentItems.FindItemGroups(targetGraph.Conventions.Patterns.ContentFiles);

            // Multiple groups can match the same framework, find all of them
            var contentFileGroupsForFramework = ContentFileUtils.GetContentGroupsForFramework(
                lockFileLib,
                framework,
                contentFileGroups);

            lockFileLib.ContentFiles = ContentFileUtils.GetContentFileGroup(
                framework,
                nuspec,
                contentFileGroupsForFramework);

            // COMPAT: Support lib/contract so older packages can be consumed
            var contractPath = "lib/contract/" + package.Id + ".dll";
            var hasContract = files.Any(path => path == contractPath);
            var hasLib = lockFileLib.RuntimeAssemblies.Any();

            if (hasContract
                && hasLib
                && !framework.IsDesktop())
            {
                lockFileLib.CompileTimeAssemblies.Clear();
                lockFileLib.CompileTimeAssemblies.Add(new LockFileItem(contractPath));
            }

            // Apply filters from the <references> node in the nuspec
            if (referenceFilter != null)
            {
                // Remove anything that starts with "lib/" and is NOT specified in the reference filter.
                // runtimes/* is unaffected (it doesn't start with lib/)
                lockFileLib.RuntimeAssemblies = lockFileLib.RuntimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(Path.GetFileName(p.Path))).ToList();
                lockFileLib.CompileTimeAssemblies = lockFileLib.CompileTimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(Path.GetFileName(p.Path))).ToList();
            }

            // Exclude items
            if ((dependencyType & LibraryIncludeFlags.Runtime) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.RuntimeAssemblies);
                lockFileLib.FrameworkAssemblies.Clear();
                lockFileLib.ResourceAssemblies.Clear();
            }

            if ((dependencyType & LibraryIncludeFlags.Compile) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.CompileTimeAssemblies);
            }

            if ((dependencyType & LibraryIncludeFlags.Native) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.NativeLibraries);
            }

            if ((dependencyType & LibraryIncludeFlags.ContentFiles) == LibraryIncludeFlags.None
                && GroupHasNonEmptyItems(lockFileLib.ContentFiles))
            {
                // Empty lock file items still need lock file properties for language, action, and output.
                lockFileLib.ContentFiles.Clear();
                lockFileLib.ContentFiles.Add(ContentFileUtils.CreateEmptyItem());
            }

            return lockFileLib;
        }

        private static bool HasItems(ContentItemGroup compileGroup)
        {
            return (compileGroup != null && compileGroup.Items.Any());
        }

        private static LockFileItem ToResourceLockFileItem(ContentItem item)
        {
            return new LockFileItem(item.Path)
            {
                Properties =
                {
                    { "locale", item.Properties["locale"].ToString()}
                }
            };
        }

        /// <summary>
        /// Clears a lock file group and replaces the first item with _._ if 
        /// the group has items. Empty groups are left alone.
        /// </summary>
        private static void ClearIfExists(IList<LockFileItem> group)
        {
            if (GroupHasNonEmptyItems(group))
            {
                // Take the root directory
                var firstItem = group.OrderBy(item => item.Path.LastIndexOf('/'))
                    .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .First();

                var fileName = Path.GetFileName(firstItem.Path);

                Debug.Assert(!string.IsNullOrEmpty(fileName));
                Debug.Assert(firstItem.Path.IndexOf('/') > 0);

                var emptyDir = firstItem.Path.Substring(0, firstItem.Path.Length - fileName.Length) 
                    + PackagingCoreConstants.EmptyFolder;

                group.Clear();

                // Create a new item with the _._ path
                var emptyItem = new LockFileItem(emptyDir);

                // Copy over the properties from the first 
                foreach (var pair in firstItem.Properties)
                {
                    emptyItem.Properties.Add(pair.Key, pair.Value);
                }

                group.Add(emptyItem);
            }
        }

        /// <summary>
        /// True if the group has items that do not end with _._
        /// </summary>
        private static bool GroupHasNonEmptyItems(IList<LockFileItem> group)
        {
            return group?.Any(item => !item.Path.EndsWith(PackagingCoreConstants.ForwardSlashEmptyFolder)) == true;
        }
    }
}

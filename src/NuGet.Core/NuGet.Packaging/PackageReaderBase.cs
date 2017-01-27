// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    /// <summary>
    /// Abstract class that both the zip and folder package readers extend
    /// This class contains the path conventions for both zip and folder readers
    /// </summary>
    public abstract class PackageReaderBase : IPackageCoreReader, IPackageContentReader
    {
        private NuspecReader _nuspecReader;
        private readonly IFrameworkNameProvider _frameworkProvider;
        private readonly IFrameworkCompatibilityProvider _compatibilityProvider;

        /// <summary>
        /// Core package reader
        /// </summary>
        /// <param name="frameworkProvider">framework mapping provider</param>
        public PackageReaderBase(IFrameworkNameProvider frameworkProvider)
            : this(frameworkProvider, new CompatibilityProvider(frameworkProvider))
        {
        }

        /// <summary>
        /// Core package reader
        /// </summary>
        /// <param name="frameworkProvider">framework mapping provider</param>
        /// <param name="compatibilityProvider">framework compatibility provider</param>
        public PackageReaderBase(IFrameworkNameProvider frameworkProvider, IFrameworkCompatibilityProvider compatibilityProvider)
            : base()
        {
            if (frameworkProvider == null)
            {
                throw new ArgumentNullException(nameof(frameworkProvider));
            }

            if (compatibilityProvider == null)
            {
                throw new ArgumentNullException(nameof(compatibilityProvider));
            }

            _frameworkProvider = frameworkProvider;
            _compatibilityProvider = compatibilityProvider;
        }

        #region IPackageCoreReader implementation

        public abstract Stream GetStream(string path);

        public abstract IEnumerable<string> GetFiles();

        public abstract IEnumerable<string> GetFiles(string folder);

        public abstract IEnumerable<string> CopyFiles(
            string destination,
            IEnumerable<string> packageFiles,
            ExtractPackageFileDelegate extractFile,
            ILogger logger,
            CancellationToken token);

        public virtual PackageIdentity GetIdentity()
        {
            return NuspecReader.GetIdentity();
        }

        public virtual NuGetVersion GetMinClientVersion()
        {
            return NuspecReader.GetMinClientVersion();
        }

        public virtual IReadOnlyList<PackageType> GetPackageTypes()
        {
            return NuspecReader.GetPackageTypes();
        }

        public virtual Stream GetNuspec()
        {
            // This is the default implementation. It is overridden and optimized in
            // PackageArchiveReader and PackageFolderReader.
            return GetStream(GetNuspecFile());
        }

        public virtual string GetNuspecFile()
        {
            // Find all nuspecs in the root folder.
            var nuspecPaths = GetFiles()
                .Where(entryPath => PackageHelper.IsManifest(entryPath))
                .ToList();

            if (nuspecPaths.Count == 0)
            {
                throw new PackagingException(Strings.MissingNuspec);
            }
            else if (nuspecPaths.Count > 1)
            {
                throw new PackagingException(Strings.MultipleNuspecFiles);
            }

            return nuspecPaths.Single();
        }

        /// <summary>
        /// Nuspec reader
        /// </summary>
        public virtual NuspecReader NuspecReader
        {
            get
            {
                if (_nuspecReader == null)
                {
                    _nuspecReader = new NuspecReader(GetNuspec());
                }

                return _nuspecReader;
            }
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);

        #endregion

        #region IPackageContentReader implementation

        /// <summary>
        /// Frameworks mentioned in the package.
        /// </summary>
        public IEnumerable<NuGetFramework> GetSupportedFrameworks()
        {
            var frameworks = new HashSet<NuGetFramework>(new NuGetFrameworkFullComparer());

            frameworks.UnionWith(GetLibItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetBuildItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetContentItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetToolItems().Select(g => g.TargetFramework));

            frameworks.UnionWith(GetFrameworkItems().Select(g => g.TargetFramework));

            return frameworks.Where(f => !f.IsUnsupported).OrderBy(f => f, new NuGetFrameworkSorter());
        }

        public IEnumerable<FrameworkSpecificGroup> GetFrameworkItems()
        {
            return NuspecReader.GetFrameworkReferenceGroups();
        }

        public bool IsServiceable()
        {
            return NuspecReader.IsServiceable();
        }

        public IEnumerable<FrameworkSpecificGroup> GetBuildItems()
        {
            var id = GetIdentity().Id;

            var results = new List<FrameworkSpecificGroup>();

            foreach (var group in GetFileGroups(PackagingConstants.Folders.Build))
            {
                var filteredGroup = group;

                if (group.Items.Any(e => !IsAllowedBuildFile(id, e)))
                {
                    // create a new group with only valid files
                    filteredGroup = new FrameworkSpecificGroup(group.TargetFramework, group.Items.Where(e => IsAllowedBuildFile(id, e)));

                    if (!filteredGroup.Items.Any())
                    {
                        // nothing was useful in the folder, skip this group completely
                        filteredGroup = null;
                    }
                }

                if (filteredGroup != null)
                {
                    results.Add(filteredGroup);
                }
            }

            return results;
        }

        /// <summary>
        /// only packageId.targets and packageId.props should be used from the build folder
        /// </summary>
        private static bool IsAllowedBuildFile(string packageId, string path)
        {
            var file = Path.GetFileName(path);

            return StringComparer.OrdinalIgnoreCase.Equals(file, String.Format(CultureInfo.InvariantCulture, "{0}.targets", packageId))
                   || StringComparer.OrdinalIgnoreCase.Equals(file, String.Format(CultureInfo.InvariantCulture, "{0}.props", packageId));
        }

        public IEnumerable<FrameworkSpecificGroup> GetToolItems()
        {
            return GetFileGroups(PackagingConstants.Folders.Tools);
        }

        public IEnumerable<FrameworkSpecificGroup> GetContentItems()
        {
            return GetFileGroups(PackagingConstants.Folders.Content);
        }

        public IEnumerable<FrameworkSpecificGroup> GetContentItems(string contentFolderName)
        {
            return GetFileGroups(contentFolderName);
        }

        public IEnumerable<PackageDependencyGroup> GetPackageDependencies()
        {
            return NuspecReader.GetDependencyGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetLibItems()
        {
            return GetFileGroups(PackagingConstants.Folders.Lib);
        }

        public IEnumerable<FrameworkSpecificGroup> GetLibItems(string libFolderName)
        {
            return GetFileGroups(libFolderName);
        }

        /// <summary>
        /// True only for assemblies that should be added as references to msbuild projects
        /// </summary>
        private static bool IsReferenceAssembly(string path)
        {
            var result = false;

            var extension = Path.GetExtension(path);

            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".dll"))
            {
                if (!path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".winmd"))
            {
                result = true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".exe"))
            {
                result = true;
            }

            return result;
        }

        public IEnumerable<FrameworkSpecificGroup> GetReferenceItems()
        {
            var referenceGroups = NuspecReader.GetReferenceGroups();
            var fileGroups = new List<FrameworkSpecificGroup>();

            // filter out non reference assemblies
            foreach (var group in GetLibItems())
            {
                fileGroups.Add(new FrameworkSpecificGroup(group.TargetFramework, group.Items.Where(e => IsReferenceAssembly(e))));
            }

            // results
            var libItems = new List<FrameworkSpecificGroup>();

            if (referenceGroups.Any())
            {
                // the 'any' group from references, for pre2.5 nuspecs this will be the only group
                var fallbackGroup = referenceGroups.Where(g => g.TargetFramework.Equals(NuGetFramework.AnyFramework)).FirstOrDefault();

                foreach (var fileGroup in fileGroups)
                {
                    // check for a matching reference group to use for filtering
                    var referenceGroup = NuGetFrameworkUtility.GetNearest<FrameworkSpecificGroup>(
                                                                           items: referenceGroups,
                                                                           framework: fileGroup.TargetFramework,
                                                                           frameworkMappings: _frameworkProvider,
                                                                           compatibilityProvider: _compatibilityProvider);

                    if (referenceGroup == null)
                    {
                        referenceGroup = fallbackGroup;
                    }

                    if (referenceGroup == null)
                    {
                        // add the lib items without any filtering
                        libItems.Add(fileGroup);
                    }
                    else
                    {
                        var filteredItems = new List<string>();

                        foreach (var path in fileGroup.Items)
                        {
                            // reference groups only have the file name, not the path
                            var file = Path.GetFileName(path);

                            if (referenceGroup.Items.Any(s => StringComparer.OrdinalIgnoreCase.Equals(s, file)))
                            {
                                filteredItems.Add(path);
                            }
                        }

                        if (filteredItems.Any())
                        {
                            libItems.Add(new FrameworkSpecificGroup(fileGroup.TargetFramework, filteredItems));
                        }
                    }
                }
            }
            else
            {
                libItems.AddRange(fileGroups);
            }

            return libItems;
        }

        public bool GetDevelopmentDependency()
        {
            return NuspecReader.GetDevelopmentDependency();
        }

        protected IEnumerable<FrameworkSpecificGroup> GetFileGroups(string folder)
        {
            var groups = new Dictionary<NuGetFramework, List<string>>(new NuGetFrameworkFullComparer());

            var isContentFolder = StringComparer.OrdinalIgnoreCase.Equals(folder, PackagingConstants.Folders.Content);
            var allowSubFolders = true;

            foreach (var path in GetFiles(folder))
            {
                // Use the known framework or if the folder did not parse, use the Any framework and consider it a sub folder
                var framework = GetFrameworkFromPath(path, allowSubFolders);

                List<string> items = null;
                if (!groups.TryGetValue(framework, out items))
                {
                    items = new List<string>();
                    groups.Add(framework, items);
                }

                items.Add(path);
            }

            // Sort the groups by framework, and the items by ordinal string compare to keep things deterministic
            foreach (var framework in groups.Keys.OrderBy(e => e, new NuGetFrameworkSorter()))
            {
                yield return new FrameworkSpecificGroup(framework, groups[framework].OrderBy(e => e, StringComparer.OrdinalIgnoreCase));
            }
        }

        private NuGetFramework GetFrameworkFromPath(string path, bool allowSubFolders = false)
        {
            var framework = NuGetFramework.AnyFramework;

            var parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // ignore paths that are too short, and ones that have additional sub directories
            if (parts.Length == 3
                || (parts.Length > 3 && allowSubFolders))
            {
                var folderName = parts[1];

                NuGetFramework parsedFramework;
                try
                {
                    parsedFramework = NuGetFramework.ParseFolder(folderName, _frameworkProvider);
                }
                catch (ArgumentException e)
                {
                    // Include package name context in the exception.
                    throw new PackagingException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.InvalidPackageFrameworkFolderName,
                            path,
                            GetIdentity()),
                        e);
                }

                if (parsedFramework.IsSpecificFramework)
                {
                    // the folder name is a known target framework
                    framework = parsedFramework;
                }
            }

            return framework;
        }

        #endregion
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// Abstract class that both the zip and folder package readers extend
    /// This class contains the path convetions for both zip and folder readers
    /// </summary>
    public abstract class PackageReaderBase : PackageReaderCoreBase
    {
        private NuspecReader _nuspec;
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
            return Nuspec.GetFrameworkReferenceGroups();
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

        public IEnumerable<PackageDependencyGroup> GetPackageDependencies()
        {
            return Nuspec.GetDependencyGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetLibItems()
        {
            return GetFileGroups(PackagingConstants.Folders.Lib);
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
            var referenceGroups = Nuspec.GetReferenceGroups();
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
            return Nuspec.GetDevelopmentDependency();
        }

        protected override sealed NuspecCoreReaderBase NuspecCore
        {
            get { return Nuspec; }
        }

        protected virtual NuspecReader Nuspec
        {
            get
            {
                if (_nuspec == null)
                {
                    _nuspec = new NuspecReader(GetNuspec());
                }

                return _nuspec;
            }
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

            yield break;
        }

        /// <summary>
        /// Return property values for the given key. Case-sensitive.
        /// </summary>
        private static IEnumerable<string> GetPropertyValues(IEnumerable<KeyValuePair<string, string>> properties, string key)
        {
            if (properties == null)
            {
                return Enumerable.Empty<string>();
            }

            if (!String.IsNullOrEmpty(key))
            {
                return properties.Select(p => p.Value);
            }

            return properties.Where(p => StringComparer.Ordinal.Equals(p.Key, key)).Select(p => p.Value);
        }

        private static string GetFileName(string path)
        {
            return path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
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

                var parsedFramework = NuGetFramework.ParseFolder(folderName, _frameworkProvider);

                if (parsedFramework.IsSpecificFramework)
                {
                    // the folder name is a known target framework
                    framework = parsedFramework;
                }
            }

            return framework;
        }

        protected abstract IEnumerable<string> GetFiles(string folder);
    }
}

using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads a development nupkg
    /// </summary>
    public class PackageReader : IPackageReader
    {
        private readonly IFileSystem _fileSystem;
        private NuspecReader _nuspec;

        public PackageReader(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Id and Version of the package
        /// </summary>
        /// <returns></returns>
        public PackageIdentity GetIdentity()
        {
            return new PackageIdentity(Nuspec.GetId(), NuGetVersion.Parse(Nuspec.GetVersion()));
        }

        /// <summary>
        /// Frameworks mentioned in the package.
        /// </summary>
        public IEnumerable<NuGetFramework> SupportedFrameworks
        {
            get
            {
                return GetLibItems().Select(g => NuGetFramework.Parse(g.TargetFramework)).Distinct(NuGetFramework.Comparer);
            }
        }

        /// <summary>
        /// The contents and dependencies of the package.
        /// </summary>
        public ComponentTree GetComponentTree()
        {
            TreeBuilder builder = new TreeBuilder();

            if (Nuspec.HasComponentGroupsNode)
            {
                builder.Add(Nuspec.GetComponentGroups());
            }
            else
            {
                // build items
                foreach (var group in GetBuildItems())
                {
                    var prop = new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, group.TargetFramework, false);

                    foreach (var item in group.Items)
                    {
                        List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
                        data.Add(new KeyValuePair<string, string>("path", item));

                        PackageItem treeItem = new DevTreeItem(PackagingConstants.Schema.TreeItemTypes.Build, true, data);

                        builder.Add(treeItem, new PackageProperty[] { prop });
                    }
                }

                // lib items
                foreach (var group in GetLibItems())
                {
                    var prop = new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, group.TargetFramework, false);

                    foreach (var item in group.Items)
                    {
                        List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
                        data.Add(new KeyValuePair<string, string>("path", item));

                        PackageItem treeItem = new DevTreeItem(PackagingConstants.Schema.TreeItemTypes.Reference, true, data);

                        builder.Add(treeItem, new PackageProperty[] { prop });
                    }
                }

                // tool items
                foreach (var group in GetToolItems())
                {
                    var prop = new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, group.TargetFramework, false);

                    foreach (var item in group.Items)
                    {
                        List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
                        data.Add(new KeyValuePair<string, string>("path", item));

                        PackageItem treeItem = new DevTreeItem(PackagingConstants.Schema.TreeItemTypes.Tool, true, data);

                        builder.Add(treeItem, new PackageProperty[] { prop });
                    }
                }

                // framework items
                foreach (var group in GetFrameworkItems())
                {
                    var prop = new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, group.TargetFramework, false);

                    foreach (var item in group.Items)
                    {
                        List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
                        data.Add(new KeyValuePair<string, string>("path", item));


                        PackageItem treeItem = new DevTreeItem(PackagingConstants.Schema.TreeItemTypes.FrameworkReference, true, data);

                        builder.Add(treeItem, new PackageProperty[] { prop });
                    }
                }

                // package dependency items
                foreach (var group in GetPackageDependencies())
                {
                    var prop = new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, group.TargetFramework, false);

                    foreach (var package in group.Packages)
                    {
                        List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
                        data.Add(new KeyValuePair<string, string>("id", package.Id));

                        VersionRange range = VersionRange.Parse(package.VersionRange);

                        data.Add(new KeyValuePair<string, string>("versionRange", range.ToString()));

                        PackageItem treeItem = new DevTreeItem(PackagingConstants.Schema.TreeItemTypes.FrameworkReference, true, data);

                        builder.Add(treeItem, new PackageProperty[] { prop });
                    }
                }
            }

            return builder.GetTree();
        }

        public IEnumerable<FrameworkSpecificGroup> GetFrameworkItems()
        {
            return Nuspec.GetFrameworkReferenceGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetBuildItems()
        {
            return GetFiles("build");
        }

        public IEnumerable<FrameworkSpecificGroup> GetToolItems()
        {
            return GetFiles("tools");
        }

        public IEnumerable<FrameworkSpecificGroup> GetContentItems()
        {
            return GetFiles("content");
        }

        public IEnumerable<PackageDependencyGroup> GetPackageDependencies()
        {
            var nuspec = new NuspecReader(GetNuspec());
            return nuspec.GetDependencyGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetLibItems()
        {
            var referenceGroups = Nuspec.GetReferenceGroups();
            var fileGroups = GetFiles("lib").ToArray();

            List<FrameworkSpecificGroup> libItems = new List<FrameworkSpecificGroup>();

            if (referenceGroups.Count() > 0)
            {
                foreach (var group in referenceGroups)
                {
                    var frameworkGroup = new FrameworkSpecificGroup(group.TargetFramework, 
                        group.Items.Select(s => 
                            s.IndexOf('/') != -1 ? s :
                            String.Format(CultureInfo.InvariantCulture, "/lib/{0}/{1}", group.TargetFramework, s)));

                    libItems.Add(frameworkGroup);
                }
            }
            else
            {
                libItems.AddRange(fileGroups);
            }

            return libItems;
        }

        private static string GetFileName(string path)
        {
            return path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        private static string GetFrameworkFromPath(string path)
        {
            string framework = PackagingConstants.AnyFramework;

            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // ignore paths that are too short, and ones that have additional sub directories
            if (parts.Length == 3)
            {
                framework = parts[1].ToLowerInvariant();

                // TODO: add support for digit only frameworks
                Match match = PackagingConstants.FrameworkRegex.Match(framework);

                if (!match.Success)
                {
                    // this is not a framework and should be ignored
                    framework = PackagingConstants.AnyFramework;
                }
            }

            return framework;
        }

        public IFileSystem FileSystem
        {
            get
            {
                return _fileSystem;
            }
        }

        private NuspecReader Nuspec
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

        private IEnumerable<FrameworkSpecificGroup> GetFiles(string folder)
        {
            Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();

            foreach (string path in FileSystem.GetFiles()
                .Where(s => s.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)))
            {
                string framework = GetFrameworkFromPath(path);

                List<string> items = null;
                if (!groups.TryGetValue(framework, out items))
                {
                    items = new List<string>();
                    groups.Add(framework, items);
                }

                items.Add(path);
            }

            foreach (string framework in groups.Keys)
            {
                yield return new FrameworkSpecificGroup(framework, groups[framework]);
            }

            yield break;
        }

        public Stream GetNuspec()
        {
            string path = _fileSystem.GetFiles().Where(f => f.EndsWith(PackagingConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            return GetStream(path);
        }

        /// <summary>
        /// Return property values for the given key. Case-sensitive.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="key"></param>
        /// <returns></returns>
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

        private Stream GetStream(string path)
        {
            Stream stream = null;

            if (!String.IsNullOrEmpty(path))
            {
                stream = FileSystem.OpenFile(path);
            }

            return stream;
        }

        public void Dispose()
        {

        }

    }
}

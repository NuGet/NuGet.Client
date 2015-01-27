using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
        private NuspecReader _nuspec;

        public PackageReader(ZipArchive zipArchive)
        {
            ZipArchive = zipArchive;
        }

        /// <summary>
        /// Id and Version of the package
        /// </summary>
        /// <returns></returns>
        public PackageIdentity GetIdentity()
        {
            return new PackageIdentity(Nuspec.GetId(), Nuspec.GetVersion());
        }

        /// <summary>
        /// Frameworks mentioned in the package.
        /// </summary>
        public IEnumerable<NuGetFramework> GetSupportedFrameworks()
        {
            var libFrameworks = GetLibItems().Select(g => g.TargetFramework).Where(tf => !tf.IsUnsupported).Distinct(NuGetFramework.Comparer);

            // TODO: improve this
            if (!libFrameworks.Any() && GetContentItems().Any())
            {
                return new NuGetFramework[] { NuGetFramework.AgnosticFramework };
            }
            else
            {
                return libFrameworks;
            }
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
            return GetFiles("lib");
        }

        private static bool IsReferenceAssembly(string path)
        {
            bool result = false;

            string extension = Path.GetExtension(path);

            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".dll") && !path.EndsWith(".resource.dll", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".winmd"))
            {
                result = true;
            }

            return result;
        }

        public IEnumerable<FrameworkSpecificGroup> GetReferenceItems()
        {
            IEnumerable<FrameworkSpecificGroup> referenceGroups = Nuspec.GetReferenceGroups();
            List<FrameworkSpecificGroup> fileGroups = new List<FrameworkSpecificGroup>();

            // filter out non reference assemblies
            foreach (var group in GetLibItems())
            {
                fileGroups.Add(new FrameworkSpecificGroup(group.TargetFramework, group.Items.Where(e => IsReferenceAssembly(e))));
            }

            // results
            List<FrameworkSpecificGroup> libItems = new List<FrameworkSpecificGroup>();

            if (referenceGroups.Any())
            {
                // the 'any' group from references, for pre2.5 nuspecs this will be the only group
                var fallbackGroup = referenceGroups.Where(g => g.TargetFramework.Equals(NuGetFramework.AnyFramework)).SingleOrDefault();

                foreach (FrameworkSpecificGroup fileGroup in fileGroups)
                {
                    // check for a matching reference group to use for filtering
                    var referenceGroup = referenceGroups.Where(g => g.TargetFramework.Equals(fileGroup.TargetFramework)).SingleOrDefault();

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
                        List<string> filteredItems = new List<string>();

                        foreach (string path in fileGroup.Items)
                        {
                            // reference groups only have the file name, not the path
                            string file = Path.GetFileName(path);

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

        private static string GetFileName(string path)
        {
            return path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        private static string GetFrameworkFromPath(string path, bool allowSubFolders=false)
        {
            string framework = PackagingConstants.AnyFramework;

            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // ignore paths that are too short, and ones that have additional sub directories
            if (parts.Length == 3 || (parts.Length > 3 && allowSubFolders))
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

        private ZipArchive ZipArchive
        {
            get;
            set;
        }

        private IEnumerable<FrameworkSpecificGroup> GetFiles(string folder)
        {
            Dictionary<NuGetFramework, List<string>> groups = new Dictionary<NuGetFramework, List<string>>(new NuGetFrameworkFullComparer());

            bool isContentFolder = StringComparer.OrdinalIgnoreCase.Equals(folder, PackagingConstants.ContentFolder);

            foreach (string path in ZipArchiveHelper.GetFiles(ZipArchive)
                .Where(f => f.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)))
            {
                NuGetFramework framework = NuGetFramework.Parse(GetFrameworkFromPath(path, isContentFolder));

                // Content allows both random folder names and framework folder names.
                // It's nearly impossible to tell the difference and stay consistent over
                // time as the frameworks change, but to make the best attempt we can
                // compare the folder name to the known frameworks
                if (isContentFolder)
                {
                    if (!framework.IsSpecificFramework)
                    {
                        framework = NuGetFramework.AnyFramework;
                    }
                }

                List<string> items = null;
                if (!groups.TryGetValue(framework, out items))
                {
                    items = new List<string>();
                    groups.Add(framework, items);
                }

                items.Add(path);
            }

            foreach (NuGetFramework framework in groups.Keys)
            {
                yield return new FrameworkSpecificGroup(framework, groups[framework]);
            }

            yield break;
        }

        public Stream GetNuspec()
        {
            string path = ZipArchiveHelper.GetFiles(ZipArchive).Where(f => f.EndsWith(PackagingConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
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
                stream = ZipArchiveHelper.OpenFile(ZipArchive, path);
            }

            return stream;
        }

        public void Dispose()
        {

        }
    }

    internal static class ZipArchiveHelper
    {
        internal static ZipArchiveEntry GetEntry(ZipArchive zipArchive, string path)
        {
            var entry = zipArchive.Entries.Where(e => e.FullName == path).FirstOrDefault();

            if(entry == null)
            {
                throw new FileNotFoundException(path);
            }

            return entry;
        }

        internal static IEnumerable<string> GetFiles(ZipArchive zipArchive)
        {
            return zipArchive.Entries.Select(e => UnescapePath(e.FullName));
        }

        private static string UnescapePath(string path)
        {
            if (path != null && path.IndexOf('%') > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        internal static Stream OpenFile(ZipArchive zipArchive, string path)
        {
            var entry = GetEntry(zipArchive, path);
            return entry.Open();
        }
    }

}

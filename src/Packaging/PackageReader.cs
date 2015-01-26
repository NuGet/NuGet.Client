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
            var libFrameworks = GetLibItems().Select(g => NuGetFramework.Parse(g.TargetFramework)).Distinct(NuGetFramework.Comparer);

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
            Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();

            foreach (string path in ZipArchiveHelper.GetFiles(ZipArchive)
                .Where(f => f.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)))
            {
                string framework = GetFrameworkFromPath(path);

                // Content allows both random folder names and framework folder names.
                // It's nearly impossible to tell the difference and stay consistent over
                // time as the frameworks change, but to make the best attempt we can
                // compare the folder name to the known frameworks
                if (StringComparer.OrdinalIgnoreCase.Equals(folder, "content"))
                {
                    if (!NuGetFramework.Parse(framework).IsSpecificFramework)
                    {
                        framework = "any";
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

            foreach (string framework in groups.Keys)
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

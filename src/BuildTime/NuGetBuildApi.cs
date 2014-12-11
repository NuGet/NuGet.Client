using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public static class NuGetBuildApi
    {

        /// <summary>
        /// Returns artifact info for packages in a packages.config that match the given set of properties.
        /// </summary>
        /// <param name="configPath">Path to packages.config</param>
        /// <param name="properties">Property values to filter on</param>
        /// <returns>Artifacts matching the property filters.</returns>
        public static IEnumerable<NuGetArtifactInfo> GetArtifactInfo(string configPath, IEnumerable<string> propertyKeys)
        {
            if (configPath == null)
            {
                throw new ArgumentNullException("configPath");
            }

            if (propertyKeys == null)
            {
                throw new ArgumentNullException("propertyKeys");
            }

            FileInfo file = new FileInfo(configPath);

            if (!file.Exists)
            {
                throw new FileNotFoundException(configPath);
            }

            List<NuGetArtifactInfo> results = new List<NuGetArtifactInfo>();

            using (FileStream stream = file.OpenRead())
            {
                ConfigReader configReader = new ConfigReader(stream);

                foreach (PackageIdentity package in configReader.GetPackages())
                {
                    // TODO: find the real path
                    string packageName = package.Id + "." + package.Version.ToString();
                    FileInfo nupkgPath = new FileInfo(Path.Combine(file.Directory.Parent.FullName, "packages",  packageName, packageName + ".nupkg"));

                    if (!nupkgPath.Exists)
                    {
                        throw new FileNotFoundException(nupkgPath.FullName);
                    }

                    NuGetPackageId id = new NuGetPackageId(package.Id, package.Version, nupkgPath.Directory.FullName);

                    ZipFileSystem zip = new ZipFileSystem(nupkgPath.OpenRead());

                    using (PackageReader packageReader = new PackageReader(zip))
                    {
                        ComponentTree tree = null;

                        // TODO: add a better check for this
                        if (packageReader.GetPackedManifest() == null)
                        {
                            using (LegacyPackageReader legacyReader = new LegacyPackageReader(zip))
                            {
                                throw new NotImplementedException();
                                //var packed = PackedManifestCreator.FromLegacy(legacyReader);
                                //tree = packed.ComponentTree;
                            }
                        }
                        else
                        {
                            tree = packageReader.GetComponentTree();
                        }

                        List<NuGetArtifactGroup> groups = new List<NuGetArtifactGroup>();

                        // TODO: use propertyKeys
                        // TODO: get full paths
                        foreach (var path in tree.GetPaths())
                        {
                            var props = path.Properties.Select(p => (KeyValueTreeProperty)p).Select(p => new KeyValuePair<string, string>(p.Key, p.Value));
                            var items = path.Items.Select(i => (NuGetTreeItem)i).Select(i => new NuGetArtifact(i.Type, i.Data.Where(p => p.Key == "path").Select(p => p.Value).Single()));

                            groups.Add(new NuGetArtifactGroup(props, items));
                        }

                        NuGetArtifactInfo info = new NuGetArtifactInfo(id, groups.ToArray());
                        results.Add(info);
                    }
                }
            }

            return results;
        }

        private static IEnumerable<KeyValuePair<string, string>> UpdateFrameworkNames(IEnumerable<KeyValuePair<string, string>> properties)
        {
            foreach (var prop in properties)
            {
                if (StringComparer.Ordinal.Equals("targetframework", prop.Key))
                {
                    NuGetFramework nuFramework = NuGetFramework.Parse(prop.Value);

                    FrameworkName frameworkName = new FrameworkName(nuFramework.FullFrameworkName);

                    KeyValuePair<string, string> fixedProp = new KeyValuePair<string,string>(prop.Key, frameworkName.ToString());

                    yield return fixedProp;
                }
                else
                {
                    yield return prop;
                }
            }

            yield break;
        }

        private static bool HasAllProperties(NuGetArtifactGroup group, IEnumerable<KeyValuePair<string, string>> properties)
        {
            HashSet<KeyValuePair<string, string>> groupProperties = new HashSet<KeyValuePair<string, string>>(group.Properties);
            HashSet<KeyValuePair<string, string>> checkProperties = new HashSet<KeyValuePair<string, string>>(properties);

            return groupProperties.IsSupersetOf(checkProperties);
        }

        public static NuGetPackageId GetPackageId(string folderPath)
        {
            NuspecReader reader = GetNuspec(folderPath);

            return CreateIdentity(reader, folderPath);
        }

        public static NuGetDependencyInfo GetDependencyInfo(FileInfo nupkgPath)
        {
            if (!nupkgPath.Exists)
            {
                throw new FileNotFoundException(nupkgPath.FullName);
            }

            using (var stream =nupkgPath.OpenRead())
            {
                ZipFileSystem zip = new ZipFileSystem(stream);

                using (PackageReader packageReader = new PackageReader(zip))
                {
                    using (var nuspecStream = packageReader.GetNuspec())
                    {
                        NuspecReader reader = new NuspecReader(nuspecStream);

                        NuGetPackageId package = CreateIdentity(reader, nupkgPath.FullName);

                        List<NuGetDependencyGroup> dependencyGroups = new List<NuGetDependencyGroup>();

                        foreach (var depGroup in reader.GetDependencyGroups())
                        {
                            FrameworkName framework = Utilities.GetFrameworkName(depGroup.TargetFramework);

                            NuGetDependency[] dependencies = depGroup.Packages.Select(d => new NuGetDependency(d.Id, VersionRange.Parse(d.VersionRange))).ToArray();

                            dependencyGroups.Add(new NuGetDependencyGroup(framework, dependencies));
                        }

                        return new NuGetDependencyInfo(package, dependencyGroups);
                    }
                }
            }
        }

        public static NuGetDependencyInfo GetDependencyInfo(string folderPath)
        {
            NuspecReader reader = GetNuspec(folderPath);

            NuGetPackageId package = CreateIdentity(reader, folderPath);

            List<NuGetDependencyGroup> dependencyGroups = new List<NuGetDependencyGroup>();

            foreach (var depGroup in reader.GetDependencyGroups())
            {
                FrameworkName framework = Utilities.GetFrameworkName(depGroup.TargetFramework);

                NuGetDependency[] dependencies = depGroup.Packages.Select(d => new NuGetDependency(d.Id, VersionRange.Parse(d.VersionRange))).ToArray();

                dependencyGroups.Add(new NuGetDependencyGroup(framework, dependencies));
            }

            return new NuGetDependencyInfo(package, dependencyGroups);
        }

        public static NuGetDependencyInfo GetDependencyInfo(NuGetPackageId package)
        {
            return GetDependencyInfo(package.Path);
        }

        private static NuspecReader GetNuspec(string folderPath)
        {
            NuspecReader reader = null;

            string file = Directory.GetFiles(folderPath, PackagingConstants.NuspecExtensionFilter, SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (String.IsNullOrEmpty(file))
            {
                throw new FileNotFoundException("unable to find nuspec");
            }

            using (Stream stream = File.OpenRead(file))
            {
                reader = new NuspecReader(stream);
            }

            return reader;
        }

        private static NuGetPackageId CreateIdentity(NuspecReader reader, string folderPath)
        {
            NuGetPackageId package = null;
            NuGetVersion version = null;

            if (NuGetVersion.TryParse(reader.GetVersion(), out version))
            {
                package = new NuGetPackageId(reader.GetId(), version, folderPath);
            }
            else
            {
                throw new InvalidDataException("invalid version");
            }

            return package;
        }
    }
}

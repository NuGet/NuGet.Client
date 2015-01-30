using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads .nuspec files
    /// </summary>
    public class NuspecReader : NuspecCoreReader
    {
        public NuspecReader(Stream stream)
            : base(stream)
        {

        }

        public NuspecReader(XDocument xml)
            : base(xml)
        {

        }

        public IEnumerable<PackageDependencyGroup> GetDependencyGroups()
        {
            string ns = Xml.Root.GetDefaultNamespace().NamespaceName;

            bool groupFound = false;

            foreach (var depGroup in Xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("dependencies", ns)).Elements(XName.Get("group", ns)))
            {
                groupFound = true;

                string groupFramework = GetAttributeValue(depGroup, "targetFramework");

                List<PackageDependency> packages = new List<PackageDependency>();

                foreach (var depNode in depGroup.Elements(XName.Get("dependency", ns)))
                {
                    VersionRange range = null;

                    var rangeNode = GetAttributeValue(depNode, "version");

                    if (!String.IsNullOrEmpty(rangeNode))
                    {
                        if (!VersionRange.TryParse(rangeNode, out range))
                        {
                            // TODO: error handling
                        }
                    }

                    packages.Add(new PackageDependency(GetAttributeValue(depNode, "id"), range));
                }

                yield return new PackageDependencyGroup(groupFramework, packages);
            }

            // legacy behavior
            if (!groupFound)
            {
                var packages = Xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("references", ns))
                    .Elements(XName.Get("reference", ns)).Select(n => new PackageDependency(GetAttributeValue(n, "id"), VersionRange.Parse(GetAttributeValue(n, "version")))).ToArray();

                if (packages.Any())
                {
                    yield return new PackageDependencyGroup(string.Empty, packages);
                }
            }

            yield break;
        }

        public IEnumerable<FrameworkSpecificGroup> GetReferenceGroups()
        {
            string ns = Xml.Root.GetDefaultNamespace().NamespaceName;

            bool groupFound = false;

            foreach (var group in Xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("references", ns)).Elements(XName.Get("group", ns)))
            {
                groupFound = true;

                string groupFramework = GetAttributeValue(group, "targetFramework");

                string[] items = group.Elements(XName.Get("reference", ns)).Select(n => GetAttributeValue(n, "file")).Where(n => !String.IsNullOrEmpty(n)).ToArray();

                if (items.Length > 0)
                {
                    yield return new FrameworkSpecificGroup(groupFramework, items);
                }
            }

            // pre-2.5 flat list of references, this should only be used if there are no groups
            if (!groupFound)
            {
                string[] items = Xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("references", ns))
                    .Elements(XName.Get("reference", ns)).Select(n => GetAttributeValue(n, "file")).Where(n => !String.IsNullOrEmpty(n)).ToArray();

                if (items.Length > 0)
                {
                    yield return new FrameworkSpecificGroup(string.Empty, items);
                }
            }

            yield break;
        }

        public IEnumerable<FrameworkSpecificGroup> GetFrameworkReferenceGroups()
        {
            string ns = Xml.Root.GetDefaultNamespace().NamespaceName;

            foreach (var group in Xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("frameworkAssemblies", ns)).Elements(XName.Get("frameworkAssembly", ns))
                .GroupBy(n => GetAttributeValue(n, "targetFramework")))
            {
                yield return new FrameworkSpecificGroup(group.Key, group.Select(n => GetAttributeValue(n, "assemblyName")).Where(n => !String.IsNullOrEmpty(n)).ToArray());
            }
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            XAttribute attribute = element.Attribute(XName.Get(attributeName));
            return attribute == null ? null : attribute.Value;
        }
    }
}

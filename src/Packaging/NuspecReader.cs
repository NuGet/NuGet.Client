using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    public class NuspecReader : INuspecReader
    {
        private readonly XDocument _xml;

        public NuspecReader(Stream stream)
            : this(XDocument.Load(stream))
        {

        }

        public NuspecReader(XDocument xml)
        {
            _xml = xml;
        }

        public string GetId()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            var node = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("id", ns)).FirstOrDefault();
            return node == null ? null : node.Value;
        }

        public string GetVersion()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            var node = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("version", ns)).FirstOrDefault();
            return node == null ? null : node.Value;
        }

        public string GetMinClientVersion()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            var node = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("minClientVersion", ns)).FirstOrDefault();
            return node == null ? null : node.Value;
        }

        public IEnumerable<KeyValuePair<string, string>> GetMetadata()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            foreach(var element in _xml.Root.Elements(XName.Get("metadata", ns)).Elements().Where(n => !n.HasElements && !String.IsNullOrEmpty(n.Value)))
            {
                yield return new KeyValuePair<string, string>(element.Name.LocalName, element.Value);
            }

            yield break;
        }

        public IEnumerable<PackageDependencyGroup> GetDependencyGroups()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            bool groupFound = false;

            foreach (var depGroup in _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("dependencies", ns)).Elements(XName.Get("group", ns)))
            {
                groupFound = true;

                string groupFramework = GetAttributeValue(depGroup, "targetFramework");

                var packages = depGroup.Elements(XName.Get("dependency", ns)).Select(n => new PackageDependency(GetAttributeValue(n, "id"), GetAttributeValue(n, "version")));

                yield return new PackageDependencyGroup(groupFramework, packages);
            }

            // legacy behavior
            if (!groupFound)
            {
                var packages = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("references", ns))
                    .Elements(XName.Get("reference", ns)).Select(n => new PackageDependency(GetAttributeValue(n, "id"), GetAttributeValue(n, "version"))).ToArray();

                if (packages.Any())
                {
                    yield return new PackageDependencyGroup(string.Empty, packages);
                }
            }

            yield break;
        }

        public IEnumerable<FrameworkSpecificGroup> GetReferenceGroups()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            bool groupFound = false;

            foreach (var group in _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("references", ns)).Elements(XName.Get("group", ns)))
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
                string[] items = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("references", ns))
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
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            foreach (var group in _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("frameworkAssemblies", ns)).Elements(XName.Get("frameworkAssembly", ns))
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

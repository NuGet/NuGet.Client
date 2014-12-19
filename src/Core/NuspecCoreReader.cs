using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.PackagingCore
{
    public class NuspecCoreReader : INuspecCoreReader
    {
        private readonly XDocument _xml;

        public NuspecCoreReader(Stream stream)
            : this(XDocument.Load(stream))
        {

        }

        public NuspecCoreReader(XDocument xml)
        {
            _xml = xml;
        }

        public string GetId()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            var node = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("id", ns)).FirstOrDefault();
            return node == null ? null : node.Value;
        }

        public NuGetVersion GetVersion()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            var node = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("version", ns)).FirstOrDefault();
            return node == null ? null : NuGetVersion.Parse(node.Value);
        }

        public SemanticVersion GetMinClientVersion()
        {
            string ns = _xml.Root.GetDefaultNamespace().NamespaceName;

            var node = _xml.Root.Elements(XName.Get("metadata", ns)).Elements(XName.Get("minClientVersion", ns)).FirstOrDefault();
            return node == null ? null : SemanticVersion.Parse(node.Value);
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

        public XDocument Xml
        {
            get
            {
                return _xml;
            }
        }
    }
}

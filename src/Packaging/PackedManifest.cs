using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// Represents nuget.packed.json
    /// This contains the compiled form of all information about the package.
    /// </summary>
    public class PackedManifest
    {
        private PackageIdentity _identity;
        private ComponentTree _componentTree;
        private readonly XDocument _xml;

        public PackedManifest(XDocument xml)
        {
            _xml = xml;
        }

        public PackageIdentity Identity
        {
            get
            {
                if (_identity == null)
                {
                    _identity = new PackageIdentity(GetElementValue("id"),
                        NuGetVersion.Parse(GetElementValue("version")));
                }

                return _identity;
            }
        }

        public ComponentTree ComponentTree
        {
            get
            {
                if (_componentTree == null)
                {
                    _componentTree = new ComponentTree(GetElement("componentTree"));
                }

                return _componentTree;
            }
        }

        public XDocument Xml
        {
            get
            {
                return _xml;
            }
        }

        // defaults to the nuget namespace
        public XElement GetElement(string name)
        {
            return Xml.Root.Elements(XName.Get(name)).SingleOrDefault();
        }

        public XElement GetElement(string name, string ns)
        {
            return Xml.Root.Elements(XName.Get(name, ns)).SingleOrDefault();
        }

        public string GetElementValue(string name)
        {
            // returns just the value of the item. If it is not a Single() nothing is returned.
            string val = null;

            var node = GetElement(name);

            if (node != null)
            {
                val = node.Value;
            }

            return val;
        }

        public string GetElementValue(string name, string ns)
        {
            // returns just the value of the item. If it is not a Single() nothing is returned.
            string val = null;

            var node = GetElement(name, ns);

            if (node != null)
            {
                val = node.Value;
            }

            return val;
        }
    }
}

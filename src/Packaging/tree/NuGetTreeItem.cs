using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    public class NuGetTreeItem : TreeItem
    {
        private readonly KeyValuePair<string, string>[] _data;
        private readonly string _type;

        public NuGetTreeItem(string type, bool required, IEnumerable<KeyValuePair<string, string>> data)
            : base(required)
        {
            _data = data.ToArray();
            _type = type;
        }

        public NuGetTreeItem(XElement xml)
            : base(xml)
        {
            List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();

            _type = xml.Name.LocalName;

            foreach (var node in xml.Elements())
            {
                data.Add(new KeyValuePair<string, string>(node.Name.LocalName, node.Value));
            }

            _data = data.ToArray();
        }

        /// <summary>
        /// Item type
        /// </summary>
        public string Type
        {
            get
            {
                return _type;
            }
        }

        /// <summary>
        /// Additional item attributes
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Data
        {
            get
            {
                return _data;
            }
        }

        public override XElement ToXml()
        {
            XAttribute required = new XAttribute(XName.Get("required", PackagingConstants.PackageCoreNamespace), Required ? true : false);

            List<object> children = new List<object>();
            children.Add(required);

            foreach (var pair in _data)
            {
                children.Add(new XElement(XName.Get(pair.Key, PackagingConstants.NuGetPackageNamespace), pair.Value));
            }

            return new XElement(XName.Get(Type, PackagingConstants.NuGetPackageNamespace), children.ToArray());
        }
    }
}

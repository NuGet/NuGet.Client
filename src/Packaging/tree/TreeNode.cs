using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// A node contains properties, items, and child nodes which continue the path.
    /// </summary>
    public class TreeNode
    {
        private readonly TreeItem[] _items;
        private readonly TreeNode[] _children;
        private readonly TreeProperty[] _properties;

        public TreeNode()
            : this(Enumerable.Empty<TreeItem>())
        {

        }

        public TreeNode(IEnumerable<TreeItem> items)
            : this(items, Enumerable.Empty<TreeProperty>())
        {

        }

        public TreeNode(IEnumerable<TreeItem> items, IEnumerable<TreeProperty> properties)
            : this(items, properties, Enumerable.Empty<TreeNode>())
        {

        }

        public TreeNode(IEnumerable<TreeItem> items, IEnumerable<TreeProperty> properties, IEnumerable<TreeNode> children)
        {
            if (items == null)
            {
                throw new ArgumentNullException("items");
            }

            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            if (children == null)
            {
                throw new ArgumentNullException("children");
            }

            _items = items.ToArray();
            _properties = properties.ToArray();
            _children = children.ToArray();
        }

        public TreeNode(XElement xml)
        {
            _items = xml.Element(XName.Get("items", PackagingConstants.PackageCoreNamespace)).Elements(XName.Get("item", PackagingConstants.PackageCoreNamespace)).Select(x => new NuGetTreeItem(x)).ToArray();

            // TODO: Add support for non keyvalue tree properties
            _properties = xml.Element(XName.Get("properties", PackagingConstants.PackageCoreNamespace)).Elements(XName.Get("property", PackagingConstants.PackageCoreNamespace)).Select(x => new KeyValueTreeProperty(x)).ToArray();

            _children = xml.Element(XName.Get("children", PackagingConstants.PackageCoreNamespace)).Elements(XName.Get("node", PackagingConstants.PackageCoreNamespace)).Select(x => new TreeNode(x)).ToArray();
        }

        /// <summary>
        /// Items that are part of this node.
        /// </summary>
        public IEnumerable<TreeItem> Items
        {
            get
            {
                return _items;
            }
        }

        /// <summary>
        /// Child nodes.
        /// </summary>
        public IEnumerable<TreeNode> Children
        {
            get
            {
                return _children;
            }
        }

        /// <summary>
        /// KeyValuePairs attached to this node.
        /// </summary>
        public IEnumerable<TreeProperty> Properties
        {
            get
            {
                return _properties;
            }
        }

        public override string ToString()
        {
            return ToXml().ToString();
        }

        public virtual XElement ToXml()
        {
            XElement items = new XElement(XName.Get("items", PackagingConstants.PackageCoreNamespace), Items.Select(i => i.ToXml()));
            XElement properties = new XElement(XName.Get("properties", PackagingConstants.PackageCoreNamespace), Properties.Select(i => i.ToXml()));
            XElement children = new XElement(XName.Get("children", PackagingConstants.PackageCoreNamespace), Children.Select(i => i.ToXml()));

            List<object> nodes = new List<object>();

            if (items.Elements().Count() > 0)
            {
                nodes.Add(items);
            }

            if (properties.Elements().Count() > 0)
            {
                nodes.Add(properties);
            }

            if (children.Elements().Count() > 0)
            {
                nodes.Add(children);
            }

            XElement root = new XElement(XName.Get("node", PackagingConstants.PackageCoreNamespace), nodes.ToArray());

            return root;
        }
    }
}

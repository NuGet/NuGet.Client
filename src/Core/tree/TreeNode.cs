using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// A node contains properties, items, and child nodes which continue the path.
    /// </summary>
    public class TreeNode
    {
        private readonly PackageItem[] _items;
        private readonly TreeNode[] _children;
        private readonly PackageProperty[] _properties;

        public TreeNode()
            : this(Enumerable.Empty<PackageItem>())
        {

        }

        public TreeNode(IEnumerable<PackageItem> items)
            : this(items, Enumerable.Empty<PackageProperty>())
        {

        }

        public TreeNode(IEnumerable<PackageItem> items, IEnumerable<PackageProperty> properties)
            : this(items, properties, Enumerable.Empty<TreeNode>())
        {

        }

        public TreeNode(IEnumerable<PackageItem> items, IEnumerable<PackageProperty> properties, IEnumerable<TreeNode> children)
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

        /// <summary>
        /// Items that are part of this node.
        /// </summary>
        public IEnumerable<PackageItem> Items
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
        public IEnumerable<PackageProperty> Properties
        {
            get
            {
                return _properties;
            }
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// A group of artifacts, dependencies, or other data with properties attached. This represents a single 
    /// path through the tree.
    /// </summary>
    public class PackageItemGroup
    {
        private readonly List<PackageProperty> _properties;
        private readonly List<PackageItem> _items;

        public PackageItemGroup(IEnumerable<PackageProperty> properties, IEnumerable<PackageItem> items)
        {
            _properties = new List<PackageProperty>(properties);
            _items = new List<PackageItem>(items);
        }

        public IEnumerable<PackageProperty> Properties
        {
            get
            {
                return _properties;
            }
        }

        public IEnumerable<PackageItem> Items
        {
            get
            {
                return _items;
            }
        }

        public PackageItemGroup Clone()
        {
            return new PackageItemGroup(this.Properties, this.Items);
        }

        public void Add(TreeNode node)
        {
            _properties.AddRange(node.Properties);
            _items.AddRange(node.Items);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// A group of artifacts, dependencies, or other data with properties attached. This represents a single 
    /// path through the tree.
    /// </summary>
    public class TreePath
    {
        private readonly List<TreeProperty> _properties;
        private readonly List<TreeItem> _items;

        public TreePath(IEnumerable<TreeProperty> properties, IEnumerable<TreeItem> items)
        {
            _properties = new List<TreeProperty>(properties);
            _items = new List<TreeItem>(items);
        }

        public IEnumerable<TreeProperty> Properties
        {
            get
            {
                return _properties;
            }
        }

        public IEnumerable<TreeItem> Items
        {
            get
            {
                return _items;
            }
        }

        public TreePath Clone()
        {
            return new TreePath(this.Properties, this.Items);
        }

        public void Add(TreeNode node)
        {
            _properties.AddRange(node.Properties);
            _items.AddRange(node.Items);
        }
    }
}

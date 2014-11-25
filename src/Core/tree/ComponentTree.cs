using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// A basic tree
    /// </summary>
    public class ComponentTree
    {
        private readonly TreeNode _root;

        public ComponentTree(TreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            _root = root;
        }

        public IEnumerable<PackageItemGroup> GetPaths()
        {
            return GetPaths(_root);
        }

        private static IEnumerable<PackageItemGroup> GetPaths(TreeNode node)
        {
            var children = node.Children.ToArray();

            if (children.Length == 0)
            {
                // this is a leaf node, create a new path
                yield return new PackageItemGroup(node.Properties, node.Items);
            }
            else
            {
                foreach (var child in children)
                {
                    foreach (var path in GetPaths(child))
                    {
                        // add ourselves in
                        path.Add(node);

                        yield return path;
                    }
                }
            }

            yield break;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}

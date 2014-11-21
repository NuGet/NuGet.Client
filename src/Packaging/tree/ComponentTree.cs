using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
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

        public ComponentTree(XElement xml)
        {
            _root = new TreeNode(xml.Element(XName.Get("node", PackagingConstants.PackageCoreNamespace)));
        }

        public IEnumerable<TreePath> GetPaths()
        {
            return GetPaths(_root);
        }

        private static IEnumerable<TreePath> GetPaths(TreeNode node)
        {
            var children = node.Children.ToArray();

            if (children.Length == 0)
            {
                // this is a leaf node, create a new path
                yield return new TreePath(node.Properties, node.Items);
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
            return ToXml().ToString();
        }

        public virtual XElement ToXml()
        {
            return new XElement(XName.Get("componentTree", PackagingConstants.PackageCoreNamespace), _root.ToXml());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    public class TreeBuilder
    {
        private readonly List<Tuple<PackageItem, List<PackageProperty>>> _items;

        public TreeBuilder()
        {
            _items = new List<Tuple<PackageItem, List<PackageProperty>>>();
        }

        public void Add(PackageItem item, IEnumerable<PackageProperty> properties)
        {
            _items.Add(new Tuple<PackageItem, List<PackageProperty>>(item, properties.ToList()));
        }

        public void Add(PackageItemGroup group)
        {
            foreach (var item in group.Items)
            {
                Add(item, group.Properties);
            }
        }

        public void Add(IEnumerable<PackageItemGroup> groups)
        {
            foreach (var group in groups)
            {
                Add(group);
            }
        }

        public ComponentTree GetTree()
        {
            var workingItems = new List<Tuple<PackageItem, List<PackageProperty>>>(_items);

            var rootItems = GetRootItems(workingItems).ToArray();

            workingItems.RemoveAll(t => rootItems.Contains(t));

            TreeNode root = new TreeNode(rootItems.Select(t => t.Item1), Enumerable.Empty<PackageProperty>(), BuildChildren(workingItems));

            return new ComponentTree(root);
        }

        private static IEnumerable<TreeNode> BuildChildren(IEnumerable<Tuple<PackageItem, List<PackageProperty>>> remainingItems)
        {
            string pivotKey = GetBestPivot(remainingItems);

            // TODO: handle items that should go under the default
            var levelItems = remainingItems.Where(t => t.Item2.Any(p => StringComparer.Ordinal.Equals(pivotKey, p.PivotKey)));

            if (remainingItems.Count() != levelItems.Count())
            {
                throw new NotImplementedException("unable to handle this type of tree");
            }

            var grouped = new Dictionary<PackageProperty, List<Tuple<PackageItem, List<PackageProperty>>>>();

            foreach (var item in levelItems)
            {
                // TODO: handle when items have multiple properties of the same key type
                var pivotProp = item.Item2.Where(p => StringComparer.Ordinal.Equals(pivotKey, p.PivotKey)).Single();

                List<Tuple<PackageItem, List<PackageProperty>>> val = null;

                if (!grouped.TryGetValue(pivotProp, out val))
                {
                    val = new List<Tuple<PackageItem, List<PackageProperty>>>();
                    grouped.Add(pivotProp, val);
                }

                val.Add(item);

                item.Item2.Remove(pivotProp);
            }

            foreach (var pivotProp in grouped.Keys)
            {
                // items with no properties go in this node
                var nodeItems = grouped[pivotProp].Where(t => !t.Item2.Any()).Select(t => t.Item1);
                var nodeProps = new PackageProperty[] { pivotProp };

                // items that still have properties go into children
                var nextLevel = grouped[pivotProp].Where(t => t.Item2.Any()).ToArray();

                IEnumerable<TreeNode> nodeChildren = BuildChildren(nextLevel);

                yield return new TreeNode(nodeItems, nodeProps, nodeChildren);
            }

            yield break;
        }

        // special case - root items
        private static IEnumerable<Tuple<PackageItem, List<PackageProperty>>> GetRootItems(IEnumerable<Tuple<PackageItem, List<PackageProperty>>> items)
        {
            foreach (var item in items)
            {
                if (item.Item2.All(p => p.IsRootLevel))
                {
                    yield return item;
                }
            }

            yield break;
        }

        // larget group of pivots
        private static string GetBestPivot(IEnumerable<Tuple<PackageItem, List<PackageProperty>>> items)
        {
            var pivotGroups = items.SelectMany(t => t.Item2).GroupBy(p => p.PivotKey)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.First().PivotKey, StringComparer.Ordinal);

            if (pivotGroups.Any())
            {
                return pivotGroups.FirstOrDefault().Select(g => g.PivotKey).FirstOrDefault();
            }

            return null;
        }

    }
}

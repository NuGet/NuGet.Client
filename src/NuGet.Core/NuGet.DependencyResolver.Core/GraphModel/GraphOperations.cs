// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NuGet.Versioning;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public static class GraphOperations
    {
        private enum WalkState
        {
            Walking,
            Rejected,
            Ambiguous
        }

        public static AnalyzeResult<TItem> Analyze<TItem>(this GraphNode<TItem> root)
        {
            var result = new AnalyzeResult<TItem>();

            root.CheckCycleAndNearestWins(result.Downgrades, result.Cycles);
            root.TryResolveConflicts(result.VersionConflicts);

            // Remove all downgrades that didn't result in selecting the node we actually downgraded to
            result.Downgrades.RemoveAll(d => d.DowngradedTo.Disposition != Disposition.Accepted);

            return result;
        }

        private static void CheckCycleAndNearestWins<TItem>(this GraphNode<TItem> root,
                                                           List<DowngradeResult<TItem>> downgrades,
                                                           List<GraphNode<TItem>> cycles)
        {
            // Cycle

            // A -> B -> A (cycle)

            // Downgrade

            // A -> B -> C -> D 2.0 (downgrage)
            //        -> D 1.0

            // Potential downgrade that turns out to not downgrade
            // This should never happen in practice since B would have never been valid to begin with.

            // A -> B -> C -> D 2.0
            //        -> D 1.0
            //   -> D 2.0

            var workingDowngrades = new Dictionary<GraphNode<TItem>, GraphNode<TItem>>();

            root.ForEach(node =>
            {
                if (node.Disposition == Disposition.Cycle)
                {
                    cycles.Add(node);

                    // Remove this node from the tree so the nothing else evaluates this.
                    // This is ok since we have a parent pointer and we can still print the path
                    node.OuterNode.InnerNodes.Remove(node);

                    return;
                }

                if (node.Disposition != Disposition.PotentiallyDowngraded)
                {
                    return;
                }

                // REVIEW: This could probably be done in a single pass where we keep track
                // of what is nearer as we walk down the graph (BFS)
                for (var n = node.OuterNode; n != null; n = n.OuterNode)
                {
                    foreach (var sideNode in n.InnerNodes)
                    {
                        if (sideNode != node && sideNode.Key.Name == node.Key.Name)
                        {
                            // Nodes that have no version range should be ignored as potential downgrades e.g. framework reference
                            if (sideNode.Key.VersionRange != null &&
                                node.Key.VersionRange != null &&
                                !RemoteDependencyWalker.IsGreaterThanOrEqualTo(sideNode.Key.VersionRange, node.Key.VersionRange))
                            {
                                workingDowngrades[node] = sideNode;
                            }
                            else
                            {
                                workingDowngrades.Remove(node);
                            }
                        }
                    }
                }

                // Remove this node from the tree so the nothing else evaluates this.
                // This is ok since we have a parent pointer and we can still print the path
                node.OuterNode.InnerNodes.Remove(node);
            });

            downgrades.AddRange(workingDowngrades.Select(p => new DowngradeResult<TItem>
            {
                DowngradedFrom = p.Key,
                DowngradedTo = p.Value
            }));
        }

        public static string GetPath<TItem>(this GraphNode<TItem> node)
        {
            var result = "";
            var current = node;

            while (current != null)
            {
                result = current.PrettyPrint() + (string.IsNullOrEmpty(result) ? "" : " -> " + result);
                current = current.OuterNode;
            }

            return result;
        }

        // A helper to navigate the graph nodes
        public static GraphNode<TItem> Path<TItem>(this GraphNode<TItem> node, params string[] path)
        {
            foreach (var item in path)
            {
                var childNode = node.InnerNodes.FirstOrDefault(n => n.Key.Name == item);

                if (childNode == null)
                {
                    return null;
                }

                node = childNode;
            }

            return node;
        }

        private static string PrettyPrint<TItem>(this GraphNode<TItem> node)
        {
            return node.Key.Name + " " + node.Key.VersionRange?.PrettyPrint();
        }

        private static bool TryResolveConflicts<TItem>(this GraphNode<TItem> root, List<VersionConflictResult<TItem>> versionConflicts)
        {
            // now we walk the tree as often as it takes to determine 
            // which paths are accepted or rejected, based on conflicts occuring
            // between cousin packages

            var acceptedLibraries = new Dictionary<string, GraphNode<TItem>>(StringComparer.OrdinalIgnoreCase);

            var patience = 1000;
            var incomplete = true;
            while (incomplete && --patience != 0)
            {
                // Create a picture of what has not been rejected yet
                var tracker = new Tracker<TItem>();

                root.ForEach(true, (node, state) =>
                    {
                        if (!state
                            || node.Disposition == Disposition.Rejected)
                        {
                            // Mark all nodes as rejected if they aren't already marked
                            node.Disposition = Disposition.Rejected;
                            return false;
                        }

                        // HACK(anurse): Reference nodes win all battles.
                        if (node.Item.Key.Type == "Reference")
                        {
                            tracker.Lock(node.Item);
                        }
                        else
                        {
                            tracker.Track(node.Item);
                        }
                        return true;
                    });

                // Inform tracker of ambiguity beneath nodes that are not resolved yet
                // between:
                // a1->b1->d1->x1
                // a1->c1->d2->z1
                // first attempt
                //  d1/d2 are considered disputed 
                //  x1 and z1 are considered ambiguous
                //  d1 is rejected
                // second attempt
                //  d1 is rejected, d2 is accepted
                //  x1 is no longer seen, and z1 is not ambiguous
                //  z1 is accepted
                root.ForEach(WalkState.Walking, (node, state) =>
                    {
                        if (node.Disposition == Disposition.Rejected)
                        {
                            return WalkState.Rejected;
                        }

                        if (state == WalkState.Walking
                            && tracker.IsDisputed(node.Item))
                        {
                            return WalkState.Ambiguous;
                        }

                        if (state == WalkState.Ambiguous)
                        {
                            tracker.MarkAmbiguous(node.Item);
                        }

                        return state;
                    });

                // Now mark unambiguous nodes as accepted or rejected
                root.ForEach(true, (node, state) =>
                    {
                        if (!state
                            || node.Disposition == Disposition.Rejected)
                        {
                            return false;
                        }

                        if (tracker.IsAmbiguous(node.Item))
                        {
                            return false;
                        }

                        if (node.Disposition == Disposition.Acceptable)
                        {
                            if (tracker.IsBestVersion(node.Item))
                            {
                                node.Disposition = Disposition.Accepted;
                                acceptedLibraries[node.Key.Name] = node;
                            }
                            else
                            {
                                node.Disposition = Disposition.Rejected;
                            }
                        }

                        return node.Disposition == Disposition.Accepted;
                    });

                incomplete = false;

                root.ForEach(node => incomplete |= node.Disposition == Disposition.Acceptable);
            }

            root.ForEach(node =>
            {
                if (node.Disposition != Disposition.Accepted)
                {
                    return;
                }

                // For all accepted nodes, find dependencies that aren't satisfied by the version
                // of the package that we have selected
                foreach (var childNode in node.InnerNodes)
                {
                    GraphNode<TItem> acceptedNode;
                    if (acceptedLibraries.TryGetValue(childNode.Key.Name, out acceptedNode) &&
                        childNode != acceptedNode &&
                        childNode.Key.VersionRange != null &&
                        string.Equals(
                            childNode.Key.TypeConstraint, 
                            acceptedNode.Key.TypeConstraint, 
                            StringComparison.Ordinal))
                    {
                        var versionRange = childNode.Key.VersionRange;
                        var checkVersion = acceptedNode.Item.Key.Version;

                        // Allow prerelease versions if the selected library is prerelease and the range is
                        // using the default behavior of filtering to stable versions.
                        // Ex: [4.0.0, ) should allow 4.0.10-beta if that library was selected during the graph walk
                        // The decision on if a prerelease version should be allowed should happen previous to this
                        // check during the walk.
                        if (checkVersion.IsPrerelease && !versionRange.IncludePrerelease)
                        {
                            versionRange = VersionRange.SetIncludePrerelease(versionRange, includePrerelease: true);
                        }

                        if (!versionRange.Satisfies(checkVersion))
                        {
                            versionConflicts.Add(new VersionConflictResult<TItem>
                            {
                                Selected = acceptedNode,
                                Conflicting = childNode
                            });
                        }
                    }
                }
            });

            return !incomplete;
        }

        public static void ForEach<TItem, TState>(this GraphNode<TItem> root, TState state, Func<GraphNode<TItem>, TState, TState> visitor)
        {
            // breadth-first walk of Node tree

            var queue = new Queue<Tuple<GraphNode<TItem>, TState>>();
            queue.Enqueue(Tuple.Create(root, state));
            while (queue.Count > 0)
            {
                var work = queue.Dequeue();
                var innerState = visitor(work.Item1, work.Item2);
                foreach (var innerNode in work.Item1.InnerNodes)
                {
                    queue.Enqueue(Tuple.Create(innerNode, innerState));
                }
            }
        }

        public static void ForEach<TItem>(this IEnumerable<GraphNode<TItem>> roots, Action<GraphNode<TItem>> visitor)
        {
            foreach (var root in roots)
            {
                root.ForEach(visitor);
            }
        }

        public static void ForEach<TItem>(this GraphNode<TItem> root, Action<GraphNode<TItem>> visitor)
        {
            // breadth-first walk of Node tree, without TState parameter
            ForEach(root, 0, (node, _) =>
                {
                    visitor(node);
                    return 0;
                });
        }

        // Box Drawing Unicode characters:
        // http://www.unicode.org/charts/PDF/U2500.pdf
        private const char LIGHT_HORIZONTAL = '\u2500';
        private const char LIGHT_UP_AND_RIGHT = '\u2514';
        private const char LIGHT_VERTICAL_AND_RIGHT = '\u251C';

        [Conditional("DEBUG")]
        public static void Dump<TItem>(this GraphNode<TItem> root, Action<string> write)
        {
            DumpNode(root, write, level: 0);
            DumpChildren(root, write, level: 0);
        }

        private static void DumpChildren<TItem>(GraphNode<TItem> root, Action<string> write, int level)
        {
            var children = root.InnerNodes;
            for (var i = 0; i < children.Count; i++)
            {
                DumpNode(children[i], write, level + 1);
                DumpChildren(children[i], write, level + 1);
            }
        }

        private static void DumpNode<TItem>(GraphNode<TItem> node, Action<string> write, int level)
        {
            var output = new StringBuilder();
            if (level > 0)
            {
                output.Append(LIGHT_VERTICAL_AND_RIGHT);
                output.Append(new string(LIGHT_HORIZONTAL, level));
                output.Append(" ");
            }

            output.Append($"{node.PrettyPrint()} ({node.Disposition})");

            if (node.Item != null
                && node.Item.Key != null)
            {
                output.Append($" => {node.Item.Key.ToString()}");
            }
            else
            {
                output.Append($" => ???");
            }
            write(output.ToString());
        }
    }
}

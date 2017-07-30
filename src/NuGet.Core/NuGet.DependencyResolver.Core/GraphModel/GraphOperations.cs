﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NuGet.LibraryModel;
using NuGet.Shared;

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

        public static AnalyzeResult<RemoteResolveResult> Analyze(this GraphNode<RemoteResolveResult> root)
        {
            var result = new AnalyzeResult<RemoteResolveResult>();

            root.CheckCycleAndNearestWins(result.Downgrades, result.Cycles);
            root.TryResolveConflicts(result.VersionConflicts);

            // Remove all downgrades that didn't result in selecting the node we actually downgraded to
            result.Downgrades.RemoveAll(d => d.DowngradedTo.Disposition != Disposition.Accepted);

            return result;
        }

        private static void CheckCycleAndNearestWins(
            this GraphNode<RemoteResolveResult> root,
            List<DowngradeResult<RemoteResolveResult>> downgrades,
            List<GraphNode<RemoteResolveResult>> cycles)
        {
            var workingDowngrades = RentDowngradesDictionary();

            root.ForEach((node, context) => WalkTreeCheckCycleAndNearestWins(context, node), ValueTuple.Create(cycles, workingDowngrades));

#if NET45
            // Increase List size for items to be added, if too small
            var requiredCapacity = downgrades.Count + workingDowngrades.Count;
            if (downgrades.Capacity < requiredCapacity)
            {
                downgrades.Capacity = requiredCapacity;
            }
#endif
            foreach (var p in workingDowngrades)
            {
                downgrades.Add(new DowngradeResult<RemoteResolveResult>
                {
                    DowngradedFrom = p.Key,
                    DowngradedTo = p.Value
                });
            }

            ReleaseDowngradesDictionary(workingDowngrades);
        }

        private static void WalkTreeCheckCycleAndNearestWins(ValueTuple<List<GraphNode<RemoteResolveResult>>, Dictionary<GraphNode<RemoteResolveResult>, GraphNode<RemoteResolveResult>>> context, GraphNode<RemoteResolveResult> node)
        {
            // Cycle:
            //
            // A -> B -> A (cycle)
            //
            // Downgrade:
            //
            // A -> B -> C -> D 2.0 (downgrade)
            //        -> D 1.0
            //
            // Potential downgrades that turns out to not be downgrades:
            //
            // 1. This should never happen in practice since B would have never been valid to begin with.
            //
            //    A -> B -> C -> D 2.0
            //           -> D 1.0
            //      -> D 2.0
            //
            // 2. This occurs if none of the sources have version C 1.0 so C 1.0 is bumped up to C 2.0.
            // 
            //   A -> B -> C 2.0
            //     -> C 1.0

            var cycles = context.Item1;
            var workingDowngrades = context.Item2;

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
                var innerNodes = n.InnerNodes;
                var count = innerNodes.Count;
                for (var i = 0; i < count; i++)
                {
                    var sideNode = innerNodes[i];
                    if (sideNode != node && StringComparer.OrdinalIgnoreCase.Equals(sideNode.Key.Name, node.Key.Name))
                    {
                        // Nodes that have no version range should be ignored as potential downgrades e.g. framework reference
                        if (sideNode.Key.VersionRange != null &&
                            node.Key.VersionRange != null &&
                            !RemoteDependencyWalker.IsGreaterThanOrEqualTo(sideNode.Key.VersionRange, node.Key.VersionRange))
                        {
                            // Is the resolved version actually within node's version range? This happen if there
                            // was a different request for a lower version of the library than this version range
                            // allows but no matching library was found, so the library is bumped up into this
                            // version range.
                            var resolvedVersion = sideNode?.Item?.Data?.Match?.Library?.Version;
                            if (resolvedVersion != null && node.Key.VersionRange.Satisfies(resolvedVersion))
                            {
                                continue;
                            }

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
                GraphNode<TItem> childNode = null;
                var innerNodes = node.InnerNodes;
                var count = innerNodes.Count;
                for (var i = 0; i < count; i++)
                {
                    var candidateNode = innerNodes[i];
                    if (StringComparer.OrdinalIgnoreCase.Equals(candidateNode.Key.Name, item))
                    {
                        childNode = candidateNode;
                        break;
                    }
                }

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
            var key = node.Item?.Key ?? node.Key;

            if (key.VersionRange == null)
            {
                return key.Name;
            }

            return key.Name + " " + key.VersionRange.ToNonSnapshotRange().PrettyPrint();
        }

        private static bool TryResolveConflicts<TItem>(this GraphNode<TItem> root, List<VersionConflictResult<TItem>> versionConflicts)
        {
            // now we walk the tree as often as it takes to determine 
            // which paths are accepted or rejected, based on conflicts occuring
            // between cousin packages

            var acceptedLibraries = Cache<TItem>.RentDictionary();

            var patience = 1000;
            var incomplete = true;

            var tracker = Cache<TItem>.RentTracker();

            while (incomplete && --patience != 0)
            {
                // Create a picture of what has not been rejected yet
                root.ForEach(true, (node, state, context) => WalkTreeRejectNodesOfRejectedNodes(state, node, context), tracker);

                // Inform tracker of ambiguity beneath nodes that are not resolved yet
                root.ForEach(WalkState.Walking, (node, state, context) => WalkTreeMarkAmbiguousNodes(node, state, context), tracker);

                // Now mark unambiguous nodes as accepted or rejected
                root.ForEach(true, (node, state, context) => WalkTreeAcceptOrRejectNodes(context, state, node), ValueTuple.Create(tracker, acceptedLibraries));

                incomplete = root.ForEachGlobalState(false, (node, state) => state || node.Disposition == Disposition.Acceptable);

                tracker.Clear();
            }

            Cache<TItem>.ReleaseTracker(tracker);

            root.ForEach((node, context) => WalkTreeDectectConflicts(node, context), ValueTuple.Create(versionConflicts, acceptedLibraries));

            Cache<TItem>.ReleaseDictionary(acceptedLibraries);

            return !incomplete;
        }

        private static void WalkTreeDectectConflicts<TItem>(GraphNode<TItem> node, ValueTuple<List<VersionConflictResult<TItem>>, Dictionary<string, GraphNode<TItem>>> context)
        {
            if (node.Disposition != Disposition.Accepted)
            {
                return;
            }

            var versionConflicts = context.Item1;
            var acceptedLibraries = context.Item2;

            // For all accepted nodes, find dependencies that aren't satisfied by the version
            // of the package that we have selected
            var innerNodes = node.InnerNodes;
            var count = innerNodes.Count;
            for (var i = 0; i < count; i++)
            {
                var childNode = innerNodes[i];
                GraphNode<TItem> acceptedNode;
                if (acceptedLibraries.TryGetValue(childNode.Key.Name, out acceptedNode) &&
                    childNode != acceptedNode &&
                    childNode.Key.VersionRange != null &&
                    acceptedNode.Item.Key.Version != null)
                {
                    var acceptedType = LibraryDependencyTargetUtils.Parse(acceptedNode.Item.Key.Type);
                    var childType = childNode.Key.TypeConstraint;

                    // Check the type constraints, if there is any overlap check for conflict
                    if ((childType & acceptedType) != LibraryDependencyTarget.None)
                    {
                        var versionRange = childNode.Key.VersionRange;
                        var checkVersion = acceptedNode.Item.Key.Version;

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
            }
        }

        private static WalkState WalkTreeMarkAmbiguousNodes<TItem>(GraphNode<TItem> node, WalkState state, Tracker<TItem> context)
        {
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
            if (node.Disposition == Disposition.Rejected)
            {
                return WalkState.Rejected;
            }

            if (state == WalkState.Walking
                && context.IsDisputed(node.Item))
            {
                return WalkState.Ambiguous;
            }

            if (state == WalkState.Ambiguous)
            {
                context.MarkAmbiguous(node.Item);
            }

            return state;
        }

        private static bool WalkTreeRejectNodesOfRejectedNodes<TItem>(bool state, GraphNode<TItem> node, Tracker<TItem> context)
        {
            if (!state || node.Disposition == Disposition.Rejected)
            {
                // Mark all nodes as rejected if they aren't already marked
                node.Disposition = Disposition.Rejected;
                return false;
            }

            context.Track(node.Item);
            return true;
        }

        private static bool WalkTreeAcceptOrRejectNodes<TItem>(ValueTuple<Tracker<TItem>, Dictionary<string, GraphNode<TItem>>> context, bool state, GraphNode<TItem> node)
        {
            var tracker = context.Item1;
            var acceptedLibraries = context.Item2;

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
        }

        private static TState ForEachGlobalState<TItem, TState>(this GraphNode<TItem> root, TState state, Func<GraphNode<TItem>, TState, TState> visitor)
        {
            var queue = Cache<TItem>.RentQueue();
            // breadth-first walk of Node tree

            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var work = queue.Dequeue();
                state = visitor(work, state);

                AddInnerNodesToQueue(work.InnerNodes, queue);
            }

            Cache<TItem>.ReleaseQueue(queue);

            return state;
        }

        private static void ForEach<TItem, TState, TContext>(this GraphNode<TItem> root, TState state, Func<GraphNode<TItem>, TState, TContext, TState> visitor, TContext context)
        {
            var queue = Cache<TItem, TState>.RentQueue();

            // breadth-first walk of Node tree
            queue.Enqueue(ValueTuple.Create(root, state));
            while (queue.Count > 0)
            {
                var work = queue.Dequeue();
                state = visitor(work.Item1, work.Item2, context);

                AddInnerNodesToQueue(work.Item1.InnerNodes, queue, state);
            }

            Cache<TItem, TState>.ReleaseQueue(queue);
        }

        public static void ForEach<TItem>(this IEnumerable<GraphNode<TItem>> roots, Action<GraphNode<TItem>> visitor)
        {
            var queue = Cache<TItem>.RentQueue();

            var graphNodes = roots.AsList();
            var count = graphNodes.Count;
            for (var g = 0; g < count; g++)
            {
                queue.Enqueue(graphNodes[g]);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    visitor(node);

                    AddInnerNodesToQueue(node.InnerNodes, queue);
                }
            }

            Cache<TItem>.ReleaseQueue(queue);
        }

        public static void ForEach<TItem>(this GraphNode<TItem> root, Action<GraphNode<TItem>> visitor)
        {
            var queue = Cache<TItem>.RentQueue();

            // breadth-first walk of Node tree, no state
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                visitor(node);

                AddInnerNodesToQueue(node.InnerNodes, queue);
            }

            Cache<TItem>.ReleaseQueue(queue);
        }

        public static void ForEach<TItem, TContext>(this GraphNode<TItem> root, Action<GraphNode<TItem>, TContext> visitor, TContext context)
        {
            var queue = Cache<TItem>.RentQueue();

            // breadth-first walk of Node tree, no state
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                visitor(node, context);

                AddInnerNodesToQueue(node.InnerNodes, queue);
            }

            Cache<TItem>.ReleaseQueue(queue);
        }

        private static void AddInnerNodesToQueue<TItem, TState>(IList<GraphNode<TItem>> innerNodes, Queue<ValueTuple<GraphNode<TItem>, TState>> queue, TState innerState)
        {
            var count = innerNodes.Count;
            for (var i = 0; i < count; i++)
            {
                var innerNode = innerNodes[i];
                queue.Enqueue(ValueTuple.Create(innerNode, innerState));
            }
        }

        private static void AddInnerNodesToQueue<TItem>(IList<GraphNode<TItem>> innerNodes, Queue<GraphNode<TItem>> queue)
        {
            var count = innerNodes.Count;
            for (var i = 0; i < count; i++)
            {
                var innerNode = innerNodes[i];
                queue.Enqueue(innerNode);
            }
        }

        [ThreadStatic]
        private static Dictionary<GraphNode<RemoteResolveResult>, GraphNode<RemoteResolveResult>> _tempDowngrades;

        public static Dictionary<GraphNode<RemoteResolveResult>, GraphNode<RemoteResolveResult>> RentDowngradesDictionary()
        {
            var dictionary = _tempDowngrades;
            if (dictionary != null)
            {
                _tempDowngrades = null;
                return dictionary;
            }

            return new Dictionary<GraphNode<RemoteResolveResult>, GraphNode<RemoteResolveResult>>();
        }

        public static void ReleaseDowngradesDictionary(Dictionary<GraphNode<RemoteResolveResult>, GraphNode<RemoteResolveResult>> dictionary)
        {
            if (_tempDowngrades == null)
            {
                dictionary.Clear();
                _tempDowngrades = dictionary;
            }
        }

        private static class Cache<TItem, TState>
        {
            [ThreadStatic]
            private static Queue<ValueTuple<GraphNode<TItem>, TState>> _queue;


            public static Queue<ValueTuple<GraphNode<TItem>, TState>> RentQueue()
            {
                var queue = _queue;
                if (queue != null)
                {
                    _queue = null;
                    return queue;
                }

                return new Queue<ValueTuple<GraphNode<TItem>, TState>>();
            }

            public static void ReleaseQueue(Queue<ValueTuple<GraphNode<TItem>, TState>> queue)
            {
                if (_queue == null)
                {
                    queue.Clear();
                    _queue = queue;
                }
            }

        }

        private static class Cache<TItem>
        {
            [ThreadStatic]
            private static Queue<GraphNode<TItem>> _queue;
            [ThreadStatic]
            private static Dictionary<string, GraphNode<TItem>> _dictionary;
            [ThreadStatic]
            private static Tracker<TItem> _tracker;

            public static Queue<GraphNode<TItem>> RentQueue()
            {
                var queue = _queue;
                if (queue != null)
                {
                    _queue = null;
                    return queue;
                }

                return new Queue<GraphNode<TItem>>();
            }

            public static void ReleaseQueue(Queue<GraphNode<TItem>> queue)
            {
                if (_queue == null)
                {
                    queue.Clear();
                    _queue = queue;
                }
            }

            public static Tracker<TItem> RentTracker()
            {
                var tracker = _tracker;
                if (tracker != null)
                {
                    _tracker = null;
                    return tracker;
                }

                return new Tracker<TItem>();
            }

            public static void ReleaseTracker(Tracker<TItem> tracker)
            {
                if (_tracker == null)
                {
                    tracker.Clear();
                    _tracker = tracker;
                }
            }

            public static Dictionary<string, GraphNode<TItem>> RentDictionary()
            {
                var dictionary = _dictionary;
                if (dictionary != null)
                {
                    _dictionary = null;
                    return dictionary;
                }

                return new Dictionary<string, GraphNode<TItem>>(StringComparer.OrdinalIgnoreCase);
            }

            public static void ReleaseDictionary(Dictionary<string, GraphNode<TItem>> dictionary)
            {
                if (_dictionary == null)
                {
                    dictionary.Clear();
                    _dictionary = dictionary;
                }
            }
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

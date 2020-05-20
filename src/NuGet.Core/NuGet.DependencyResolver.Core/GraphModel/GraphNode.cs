// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public class GraphNode<TItem>
    {
        public GraphNode(LibraryRange key)
        {
            Key = key;
            InnerNodes = new List<GraphNode<TItem>>();
            Disposition = Disposition.Acceptable;
            ParentNodes = new BlockingCollection<GraphNode<TItem>>();
        }

        public LibraryRange Key { get; set; }
        public GraphItem<TItem> Item { get; set; }
        public GraphNode<TItem> OuterNode { get; set; }
        public IList<GraphNode<TItem>> InnerNodes { get; set; }
        public Disposition Disposition { get; set; }

        /// <summary>
        /// Used in case that a node is removed from its outernode and needs to keep reference of its parents.
        /// </summary>
        internal BlockingCollection<GraphNode<TItem>> ParentNodes { get; }

        /// <summary>
        /// For a node that has an <see cref="Disposition.Acceptable"/> <see cref="Disposition"/>
        /// If all its parents are Rejected the node <see cref="Disposition"/> will be changed to <see cref="Disposition.Rejected"/>
        /// </summary>
        internal bool AreParentsRejected()
        {
            if (ParentNodes.Count == 0)
            {
                return false;
            }

            return ParentNodes.IsAddingCompleted && !ParentNodes.Where(parent => parent.Disposition != Disposition.Rejected).Any();
        }

        public override string ToString()
        {
            return (Item?.Key ?? Key) + " " + Disposition;
        }
    }
}

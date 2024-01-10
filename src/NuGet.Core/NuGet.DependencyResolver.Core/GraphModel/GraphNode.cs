// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public class GraphNode<TItem>
    {
        public GraphNode(LibraryRange key)
            : this(key, hasInnerNodes: true, hasParentNodes: true)
        {
        }

        internal GraphNode(LibraryRange key, bool hasInnerNodes, bool hasParentNodes)
        {
            Key = key;
            Disposition = Disposition.Acceptable;

            InnerNodes = hasInnerNodes ? new List<GraphNode<TItem>>() : EmptyList;
            ParentNodes = hasParentNodes ? new List<GraphNode<TItem>>() : EmptyList;
        }

        //All empty ParentNodes and InnerNodes point to this EmptyList, to reduce the memory allocation for empty ParentNodes and InnerNodes
        internal static readonly IList<GraphNode<TItem>> EmptyList = Array.Empty<GraphNode<TItem>>();
        public LibraryRange Key { get; set; }
        public GraphItem<TItem> Item { get; set; }
        public GraphNode<TItem> OuterNode { get; set; }
        public IList<GraphNode<TItem>> InnerNodes { get; set; }
        public Disposition Disposition { get; set; }

        /// <summary>
        /// Used in case that a node is removed from its outernode and needs to keep reference of its parents.
        /// </summary>
        public IList<GraphNode<TItem>> ParentNodes { get; }

        internal bool AreAllParentsRejected()
        {
            var pCount = ParentNodes.Count;
            if (pCount == 0)
            {
                return false;
            }

            for (int i = 0; i < pCount; i++)
            {
                if (ParentNodes[i].Disposition != Disposition.Rejected)
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            return (Item?.Key ?? Key) + " " + Disposition;
        }
    }
}

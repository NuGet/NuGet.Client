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
        public GraphNode(LibraryRange key, bool hasEmptyInnerNodes = false, bool hasEmptyParentNodes = false)
        {
            Key = key;
            Disposition = Disposition.Acceptable;

            //Create nonEmpty InnerNodes only when it's nessecery (InnerNodes is empty when it has no dependencies, including runtime dependencies).
            if (hasEmptyInnerNodes)
            {
                InnerNodes = EmptyList;
            }
            else
            {
                InnerNodes = new List<GraphNode<TItem>>();
            }

            //Create nonEmpty ParentNodes only when it's nessecery (ParentNodes is nonEmpty only for certain nodes when Central Package Management is enabled).
            if (hasEmptyParentNodes)
            {
                ParentNodes = EmptyList;
            }
            else
            {
                ParentNodes = new List<GraphNode<TItem>>();
            }
        }

        //All empty ParentNodes and InnerNodes point to this immutable EmptyList, to reduce the memory allocation for empty ParentNodes and InnerNodes
        internal static readonly IList<GraphNode<TItem>> EmptyList = new List<GraphNode<TItem>>(0).AsReadOnly();
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

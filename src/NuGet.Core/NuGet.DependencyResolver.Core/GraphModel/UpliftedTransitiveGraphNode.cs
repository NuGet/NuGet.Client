// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{

    public class UpliftedTransitiveGraphNode<TItem> : GraphNode<TItem>
    {
        private Disposition _disposition = Disposition.Acceptable;

        private UpliftedTransitiveGraphNode(LibraryRange key) : base(key)
        {
            ParentNodes = new List<GraphNode<TItem>>();
        }

        public IList<GraphNode<TItem>> ParentNodes { get; }

        public override Disposition Disposition
        {
            get
            {
                if (ParentNodes.Where(fosterParent => fosterParent.Disposition != Disposition.Rejected).Any())
                {
                    return _disposition;
                }

                return Disposition.Rejected;
            }
            set
            {
                _disposition = value;
            }
        }

        public static UpliftedTransitiveGraphNode<TItem> Create(GraphNode<TItem> node)
        {
            UpliftedTransitiveGraphNode<TItem> result = new UpliftedTransitiveGraphNode<TItem>(node.Key);
            result.Disposition = node.Disposition;
            result.InnerNodes = node.InnerNodes;
            result.OuterNode = node.OuterNode;
            result.Item = node.Item;

            return result;
        }
    }
}

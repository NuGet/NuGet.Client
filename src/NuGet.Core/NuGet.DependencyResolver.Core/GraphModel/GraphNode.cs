﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
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
        }

        public LibraryRange Key { get; set; }
        public GraphItem<TItem> Item { get; set; }
        public GraphNode<TItem> OuterNode { get; set; }
        public IList<GraphNode<TItem>> InnerNodes { get; set; }
        public Disposition Disposition { get; set; }

        public override string ToString()
        {
            return (Item?.Key ?? Key) + " " + Disposition;
        }
    }
}

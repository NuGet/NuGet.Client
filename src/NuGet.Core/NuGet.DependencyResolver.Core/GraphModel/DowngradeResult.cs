// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.DependencyResolver
{
    public class DowngradeResult<TItem>
    {
        public GraphNode<TItem> DowngradedFrom { get; set; }
        public GraphNode<TItem> DowngradedTo { get; set; }
    }
}

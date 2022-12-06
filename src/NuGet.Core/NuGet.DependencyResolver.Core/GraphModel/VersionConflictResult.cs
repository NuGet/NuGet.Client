// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.DependencyResolver
{
    public class VersionConflictResult<TItem>
    {
        public GraphNode<TItem> Selected { get; set; }
        public GraphNode<TItem> Conflicting { get; set; }
    }
}

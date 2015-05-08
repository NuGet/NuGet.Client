// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    [DebuggerDisplay("{Key}")]
    public class GraphItem<TItem> : IEquatable<GraphItem<TItem>>
    {
        public GraphItem(LibraryIdentity key)
        {
            Key = key;
        }

        public LibraryIdentity Key { get; set; }
        public TItem Data { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as GraphItem<TItem>);
        }

        public bool Equals(GraphItem<TItem> other)
        {
            return other != null && Key.Equals(other.Key);
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}

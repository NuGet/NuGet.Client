// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using NuGet.LibraryModel;
using NuGet.Shared;

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

        /// <summary>
        /// A <see cref="LibraryDependency"/> created from a <see cref="CentralPackageVersion"/>.
        /// If set it will indicate that the graph node was created due to a transitive dependency for a package that was also defined in the central packages management file. 
        /// </summary>
        public LibraryDependency CentralDependency { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as GraphItem<TItem>);
        }

        public bool Equals(GraphItem<TItem> other)
        {
            return other != null &&
                KeyCompare(other) &&
                CentralDependencyCompare(other);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.AddObject(Key);
            combiner.AddObject(CentralDependency);

            return combiner.CombinedHash;
        }

        private bool KeyCompare(GraphItem<TItem> other)
        {
            if (Key == null)
            {
                return other.Key == null;
            }
            return Key.Equals(other.Key);
        }

        private bool CentralDependencyCompare(GraphItem<TItem> other)
        {
            if (CentralDependency == null)
            {
                return other.CentralDependency == null;
            }
            return CentralDependency.Equals(other.CentralDependency);
        }
    }
}

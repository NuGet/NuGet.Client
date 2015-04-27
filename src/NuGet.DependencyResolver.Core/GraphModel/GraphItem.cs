using System.Diagnostics;
using NuGet.LibraryModel;
using System;

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
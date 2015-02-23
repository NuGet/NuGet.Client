using System.Diagnostics;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    [DebuggerDisplay("{Key}")]
    public class GraphItem<TItem>
    {
        public GraphItem(LibraryIdentity key)
        {
            Key = key;
        }

        public LibraryIdentity Key { get; set; }
        public TItem Data { get; set; }
    }
}
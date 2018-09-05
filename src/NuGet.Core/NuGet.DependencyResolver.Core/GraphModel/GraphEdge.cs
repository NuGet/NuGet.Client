using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    /// <summary>
    /// GraphEdge holds a reference to the parent node, the incoming edge to the parent, and
    /// the out going edge to the current position of the walk.
    /// 
    /// Root -> OuterEdge -> Node -> Edge -> (Current Node)
    /// </summary>
    public class GraphEdge<TItem>
    {
        public GraphEdge(GraphEdge<TItem> outerEdge, GraphItem<TItem> item, LibraryDependency edge)
        {
            OuterEdge = outerEdge;
            Item = item;
            Edge = edge;
        }

        /// <summary>
        /// Incoming edge to <see cref="Item"/>.
        /// </summary>
        public GraphEdge<TItem> OuterEdge { get; }

        /// <summary>
        /// Graph node between <see cref="OuterEdge"/> and <see cref="Edge"/>.
        /// </summary>
        public GraphItem<TItem> Item { get; }

        /// <summary>
        /// Outgoing edge from <see cref="Item"/>.
        /// </summary>
        public LibraryDependency Edge { get; }
    }
}

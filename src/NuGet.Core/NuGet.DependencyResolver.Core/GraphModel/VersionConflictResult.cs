
namespace NuGet.DependencyResolver
{
    public class VersionConflictResult<TItem>
    {
        public GraphNode<TItem> Selected { get; set; } = null!;
        public GraphNode<TItem> Conflicting { get; set; } = null!;
    }
}

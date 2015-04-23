using NuGet.DependencyResolver;
using NuGet.Frameworks;

namespace NuGet.Strawman.Commands
{
    public class RestoreGraph
    {
        public string RuntimeIdentifier { get; }
        public NuGetFramework Framework { get; }
        public GraphNode<RemoteResolveResult> Graph { get; }

        public RestoreGraph(string runtimeIdentifier, NuGetFramework framework, GraphNode<RemoteResolveResult> graph)
        {
            RuntimeIdentifier = runtimeIdentifier;
            Framework = framework;
            Graph = graph;
        }
    }
}
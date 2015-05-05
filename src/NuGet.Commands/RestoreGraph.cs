using System;
using System.Collections.Generic;
using NuGet.Client;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.RuntimeModel;

namespace NuGet.Commands
{
    public class RestoreTargetGraph
    {
        /// <summary>
        /// Gets the runtime identifier used during the restore operation on this graph
        /// </summary>
        public string RuntimeIdentifier { get; }

        /// <summary>
        /// Gets the <see cref="NuGetFramework"/> used during the restore operation on this graph
        /// </summary>
        public NuGetFramework Framework { get; }

        /// <summary>
        /// Gets the <see cref="ManagedCodeConventions"/> used to resolve assets from packages in this graph
        /// </summary>
        public ManagedCodeConventions Conventions { get; }

        /// <summary>
        /// Gets the <see cref="RuntimeGraph"/> that defines runtimes and their relationships for this graph
        /// </summary>
        public RuntimeGraph RuntimeGraph { get; }

        /// <summary>
        /// Gets the resolved dependency graph
        /// </summary>
        public GraphNode<RemoteResolveResult> Graph { get; }

        public ISet<RemoteMatch> Install { get; }
        public ISet<GraphItem<RemoteResolveResult>> Flattened { get; }
        public ISet<LibraryRange> Unresolved { get; }
        public bool InConflict { get; }

        private RestoreTargetGraph(bool inConflict, NuGetFramework framework, string runtimeIdentifier, RuntimeGraph runtimeGraph, GraphNode<RemoteResolveResult> graph, ISet<RemoteMatch> install, ISet<GraphItem<RemoteResolveResult>> flattened, ISet<LibraryRange> unresolved)
        {
            InConflict = inConflict;
            RuntimeIdentifier = runtimeIdentifier;
            RuntimeGraph = runtimeGraph;
            Framework = framework;
            Graph = graph;

            Conventions = new ManagedCodeConventions(runtimeGraph);

            Install = install;
            Flattened = flattened;
            Unresolved = unresolved;
        }

        public static RestoreTargetGraph Create(bool inConflict, NuGetFramework framework, GraphNode<RemoteResolveResult> graph, RemoteWalkContext context, ILogger logger)
        {
            return Create(inConflict, framework, null, RuntimeGraph.Empty, graph, context, logger);
        }

        public static RestoreTargetGraph Create(
            bool inConflict,
            NuGetFramework framework,
            string runtimeIdentifier,
            RuntimeGraph runtimeGraph,
            GraphNode<RemoteResolveResult> graph,
            RemoteWalkContext context,
            ILogger log)
        {
            var install = new HashSet<RemoteMatch>();
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>();
            var unresolved = new HashSet<LibraryRange>();

            graph.ForEach(node =>
            {
                if (node == null || node.Key == null || node.Disposition == Disposition.Rejected)
                {
                    return;
                }

                if (node.Item == null || node.Item.Data.Match == null)
                {
                    if (node.Key.TypeConstraint != LibraryTypes.Reference &&
                        node.Key.VersionRange != null)
                    {
                        unresolved.Add(node.Key);
                    }

                    return;
                }

                if (!string.Equals(node.Item.Data.Match.Library.Name, node.Key.Name, StringComparison.Ordinal))
                {
                    // Fix casing of the library name to be installed
                    node.Item.Data.Match.Library.Name = node.Key.Name;
                }

                // If the package came from a remote library provider, it needs to be installed locally
                var isRemote = context.RemoteLibraryProviders.Contains(node.Item.Data.Match.Provider);
                if (isRemote)
                {
                    install.Add(node.Item.Data.Match);
                }

                flattened.Add(node.Item);
            });

            return new RestoreTargetGraph(
                inConflict,
                framework,
                runtimeIdentifier,
                runtimeGraph,
                graph,
                install,
                flattened,
                unresolved);
        }
    }
}
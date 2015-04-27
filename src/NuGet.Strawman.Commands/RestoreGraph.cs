using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using System.Collections.Generic;
using System;
using Microsoft.Framework.Logging;
using System.Linq;
using NuGet.RuntimeModel;
using NuGet.Client;

namespace NuGet.Strawman.Commands
{
    public class RestoreTargetGraph
    {
        public string RuntimeIdentifier { get; }
        public ManagedCodeConventions Conventions { get; }
        public RuntimeGraph RuntimeGraph { get; }
        public NuGetFramework Framework { get; }
        public GraphNode<RemoteResolveResult> Graph { get; }

        public HashSet<LibraryIdentity> Libraries { get; private set; }
        public HashSet<LibraryRange> Unresolved { get; private set; }

        public RestoreTargetGraph(string runtimeIdentifier, RuntimeGraph runtimeGraph, NuGetFramework framework, GraphNode<RemoteResolveResult> graph)
        {
            RuntimeIdentifier = runtimeIdentifier;
            RuntimeGraph = runtimeGraph;
            Framework = framework;
            Graph = graph;

            Conventions = new ManagedCodeConventions(runtimeGraph);
        }

        public bool Flatten(RemoteWalkContext context, IList<RemoteMatch> toInstall, IList<GraphItem<RemoteResolveResult>> flattened, ILoggerFactory loggerFactory)
        {
            var log = loggerFactory.CreateLogger<RestoreTargetGraph>();

            var libraries = new HashSet<LibraryIdentity>();
            var unresolved = new HashSet<LibraryRange>();

            bool success = true;

            Graph.ForEach(node =>
            {
                if (node == null || node.Key == null || node.Disposition == Disposition.Rejected)
                {
                    return;
                }

                if (node.Item == null || node.Item.Data.Match == null)
                {
                    if (node.Key.TypeConstraint != LibraryTypes.Reference &&
                        node.Key.VersionRange != null &&
                        unresolved.Add(node.Key))
                    {
                        var errorMessage = string.Format("Unable to locate {0} {1}",
                            node.Key.Name,
                            node.Key.VersionRange);
                        log.LogError(errorMessage);
                        success = false;
                    }

                    return;
                }

                if (!string.Equals(node.Item.Data.Match.Library.Name, node.Key.Name, StringComparison.Ordinal))
                {
                    // Fix casing of the library name to be installed
                    node.Item.Data.Match.Library.Name = node.Key.Name;
                }

                var isRemote = context.RemoteLibraryProviders.Contains(node.Item.Data.Match.Provider);
                var isAdded = toInstall.Any(item => item.Library == node.Item.Data.Match.Library);

                if (!isAdded && isRemote)
                {
                    toInstall.Add(node.Item.Data.Match);
                }

                var isGraphItem = flattened.Any(item => item.Data.Match.Library == node.Item.Data.Match.Library);
                if (!isGraphItem)
                {
                    flattened.Add(node.Item);
                }

                libraries.Add(node.Item.Key);
            });

            Libraries = libraries;
            Unresolved = unresolved;

            return success;
        }
    }
}
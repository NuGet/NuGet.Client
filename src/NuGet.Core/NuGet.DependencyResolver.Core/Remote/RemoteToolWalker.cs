// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.DependencyResolver
{
    /// <summary>
    /// Walks a single package or single level of dependencies.
    /// </summary>
    public class RemoteToolWalker
    {
        private readonly RemoteWalkContext _context;

        // Cache tool dependencies between frameworks
        private readonly ConcurrentDictionary<LibraryRange, Task<GraphItem<RemoteResolveResult>>> _cache
            = new ConcurrentDictionary<LibraryRange, Task<GraphItem<RemoteResolveResult>>>();

        public RemoteToolWalker(RemoteWalkContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Return a single package, this will not add or walk dependencies.
        /// </summary>
        public async Task<GraphNode<RemoteResolveResult>> GetNodeAsync(LibraryRange library, CancellationToken token)
        {
            var match = await RemoteMatchUtility.FindLibraryMatch(
                libraryRange: library,
                framework: NuGetFramework.AgnosticFramework,
                outerEdge: null,
                context: _context,
                cancellationToken: token);

            if (match != null)
            {
                var node = new GraphNode<RemoteResolveResult>(library);

                node.Item = new GraphItem<RemoteResolveResult>(match.Library)
                {
                    Data = new RemoteResolveResult()
                    {
                        Match = match,
                        Dependencies = Enumerable.Empty<LibraryDependency>()
                    }
                };

                return node;
            }
            else
            {
                // Unable to find tool
                var node = new GraphNode<RemoteResolveResult>(library)
                {
                    Item = RemoteMatchUtility.CreateUnresolvedMatch(library)
                };

                return node;
            }
        }

        /// <summary>
        /// Populate all dependencies.
        /// </summary>
        public async Task WalkAsync(IEnumerable<GraphNode<RemoteResolveResult>> nodes, CancellationToken token)
        {
            // Resolve dependencies of the tool package
            // Wait for all packages to be resolved
            await Task.WhenAll(nodes.Select(root => ApplyDependencyNodes(root, token)));
        }

        private async Task ApplyDependencyNodes(GraphNode<RemoteResolveResult> root, CancellationToken token)
        {
            var tasks = root.Item.Data.Dependencies
                .Select(dependency => GetDependencyNode(dependency.LibraryRange, token))
                .ToList();

            while (tasks.Count > 0)
            {
                // Wait for any node to finish resolving
                var task = await Task.WhenAny(tasks);

                // Extract the resolved node
                tasks.Remove(task);
                var dependencyNode = await task;
                dependencyNode.OuterNode = root;

                root.InnerNodes.Add(dependencyNode);
            }
        }

        private async Task<GraphNode<RemoteResolveResult>> GetDependencyNode(LibraryRange library, CancellationToken token)
        {
            return new GraphNode<RemoteResolveResult>(library)
            {
                Item = await FindToolDependencyCached(
                    libraryRange: library,
                    token: token)
            };
        }

        private Task<GraphItem<RemoteResolveResult>> FindToolDependencyCached(
            LibraryRange libraryRange,
            CancellationToken token)
        {
            return _cache.GetOrAdd(libraryRange, (cacheKey) => FindToolDependency(libraryRange, token));
        }

        private async Task<GraphItem<RemoteResolveResult>> FindToolDependency(LibraryRange library, CancellationToken token)
        {
            GraphItem<RemoteResolveResult> item = null;

            var match = await RemoteMatchUtility.FindLibraryMatch(
                libraryRange: library,
                framework: NuGetFramework.AgnosticFramework,
                outerEdge: null,
                context: _context,
                cancellationToken: token);

            if (match != null)
            {
                // Dependencies are ignored here
                item = new GraphItem<RemoteResolveResult>(match.Library)
                {
                    Data = new RemoteResolveResult()
                    {
                        Match = match,
                        Dependencies = Enumerable.Empty<LibraryDependency>()
                    }
                };
            }
            else
            {
                // Unable to find package
                item = RemoteMatchUtility.CreateUnresolvedMatch(library);
            }

            return item;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Shared;

namespace NuGet.Commands
{
    internal static class TransitiveNoWarnUtils
    {
        internal static IDictionary<string, WarningPropertiesCollection> CreateTransitiveWarningPropertiesCollection(
            DependencyGraphSpec dgSpec,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            LibraryIdentity parentProject)
        {
            var transitiveWarningPropertiesCollection = new Dictionary<string, WarningPropertiesCollection>();

            foreach (var targetGraph in targetGraphs)
            {
                if (string.IsNullOrEmpty(targetGraph.RuntimeIdentifier))
                {
                    TraverseTargetGraph(targetGraph, parentProject);
                }
                
            }

            return transitiveWarningPropertiesCollection;
        }

        private static void TraverseTargetGraph(RestoreTargetGraph targetGraph, LibraryIdentity parentProject)
        {
            var paths = new Dictionary<string, IEnumerable<IEnumerable<LibraryIdentity>>>();
            var dependencyMapping = new Dictionary<string, IEnumerable<LibraryDependency>>();
            var queue = new Queue<Tuple<string, IEnumerable<LibraryIdentity>>>();
            var seen = new HashSet<string>();

            // seed the queue with the original flattened graph
            // Add all dependencies into a dict for a quick transitive lookup
            foreach (var dependency in targetGraph.Flattened.OrderBy(d => d.Key.Name))
            {
                dependencyMapping[dependency.Key.Name] = dependency.Data.Dependencies;
                queue.Enqueue(Tuple.Create<string, IEnumerable<LibraryIdentity>>(dependency.Key.Name, new List<LibraryIdentity> { parentProject }));
            }

            // seed the queue with the original flattened graph
            // Add all dependencies into a dict for a quick transitive lookup
            foreach (var dependency in targetGraph.Flattened.OrderBy(d => d.Key.Name))
            {
                dependencyMapping[dependency.Key.Name] = dependency.Data.Dependencies;
                queue.Enqueue(Tuple.Create<string, IEnumerable<LibraryIdentity>>(dependency.Key.Name, new List<LibraryIdentity> { parentProject }));
            }

            var path = new List<LibraryIdentity> { parentProject };
            // start taking one node from the queue and get all of it's dependencies
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                
            }
        }
    }
}

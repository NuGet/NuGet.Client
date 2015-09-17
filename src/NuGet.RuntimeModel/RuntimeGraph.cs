// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NuGet.RuntimeModel
{
    public class RuntimeGraph : IEquatable<RuntimeGraph>
    {
        private ConcurrentDictionary<Tuple<string, string>, bool> _areCompatible 
            = new ConcurrentDictionary<Tuple<string, string>, bool>();

        public static readonly string RuntimeGraphFileName = "runtime.json";

        public static readonly RuntimeGraph Empty = new RuntimeGraph();

        public IReadOnlyDictionary<string, RuntimeDescription> Runtimes { get; }
        public IReadOnlyDictionary<string, CompatibilityProfile> Supports { get; set; }

        private RuntimeGraph()
            : this(Enumerable.Empty<RuntimeDescription>(), Enumerable.Empty<CompatibilityProfile>())
        {
        }

        public RuntimeGraph(IEnumerable<RuntimeDescription> runtimes)
            : this(runtimes, Enumerable.Empty<CompatibilityProfile>())
        {
        }

        public RuntimeGraph(IEnumerable<CompatibilityProfile> supports)
            : this(Enumerable.Empty<RuntimeDescription>(), supports)
        {
        }

        public RuntimeGraph(IEnumerable<RuntimeDescription> runtimes, IEnumerable<CompatibilityProfile> supports)
            : this(runtimes.ToDictionary(r => r.RuntimeIdentifier), supports.ToDictionary(r => r.Name))
        {
        }

        private RuntimeGraph(Dictionary<string, RuntimeDescription> runtimes, Dictionary<string, CompatibilityProfile> supports)
        {
            Runtimes = new ReadOnlyDictionary<string, RuntimeDescription>(runtimes);
            Supports = new ReadOnlyDictionary<string, CompatibilityProfile>(supports);
        }

        public RuntimeGraph Clone()
        {
            return new RuntimeGraph(Runtimes.Values.Select(r => r.Clone()));
        }

        /// <summary>
        /// Merges the content of the other runtime graph in to this runtime graph
        /// </summary>
        /// <param name="other">The other graph to merge in to this graph</param>
        public static RuntimeGraph Merge(RuntimeGraph left, RuntimeGraph right)
        {
            var runtimes = new Dictionary<string, RuntimeDescription>();
            foreach (var runtime in left.Runtimes.Values)
            {
                runtimes[runtime.RuntimeIdentifier] = runtime.Clone();
            }

            // Merge the right-side runtimes
            foreach (var runtime in right.Runtimes.Values)
            {
                // Check if we already have the runtime defined
                RuntimeDescription leftRuntime;
                if (runtimes.TryGetValue(runtime.RuntimeIdentifier, out leftRuntime))
                {
                    // Merge runtimes
                    runtimes[runtime.RuntimeIdentifier] = RuntimeDescription.Merge(leftRuntime, runtime);
                }
                else
                {
                    runtimes[runtime.RuntimeIdentifier] = runtime;
                }
            }

            // Copy over the right ones
            var supports = new Dictionary<string, CompatibilityProfile>();
            foreach (var compatProfile in right.Supports)
            {
                supports[compatProfile.Key] = compatProfile.Value;
            }

            // Overwrite with non-empty profiles from left
            foreach (var compatProfile in left.Supports.Where(p => p.Value.RestoreContexts.Any()))
            {
                supports[compatProfile.Key] = compatProfile.Value;
            }

            return new RuntimeGraph(runtimes, supports);
        }

        public IEnumerable<string> ExpandRuntime(string runtime)
        {
            // Could this be faster? Sure! But we can refactor once it works and has tests
            yield return runtime;

            // Try to expand the runtime based on the graph
            var deduper = new HashSet<string>();
            var expansions = new List<string>();
            deduper.Add(runtime);
            expansions.Add(runtime);
            for (var i = 0; i < expansions.Count; i++)
            {
                // expansions.Count will keep growing as we add items, but thats OK, we want to expand until we stop getting new items
                RuntimeDescription desc;
                if (Runtimes.TryGetValue(expansions[i], out desc))
                {
                    // Add the inherited runtimes to the list
                    foreach (var inheritedRuntime in desc.InheritedRuntimes)
                    {
                        if (deduper.Add(inheritedRuntime))
                        {
                            yield return inheritedRuntime;
                            expansions.Add(inheritedRuntime);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines if two runtime identifiers are compatible, based on the import graph
        /// </summary>
        /// <param name="criteria">The criteria being tested</param>
        /// <param name="provided">The value the criteria is being tested against</param>
        /// <returns>
        /// true if an asset for the runtime in <paramref name="provided" /> can be installed in a project
        /// targetting <paramref name="criteria" />, false otherwise
        /// </returns>
        public bool AreCompatible(string criteria, string provided)
        {
            var key = new Tuple<string, string>(criteria, provided);

            return _areCompatible.GetOrAdd(key, (tuple) => ExpandRuntime(tuple.Item1).Contains(tuple.Item2));
        }

        public IEnumerable<RuntimePackageDependency> FindRuntimeDependencies(string runtimeName, string packageId)
        {
            // PERF: We could cache this for a particular (runtimeName,packageId) pair.
            foreach (var expandedRuntime in ExpandRuntime(runtimeName))
            {
                RuntimeDescription runtimeDescription;
                if (Runtimes.TryGetValue(expandedRuntime, out runtimeDescription))
                {
                    RuntimeDependencySet dependencySet;
                    if (runtimeDescription.RuntimeDependencySets.TryGetValue(packageId, out dependencySet))
                    {
                        return dependencySet.Dependencies.Values;
                    }
                }
            }
            return Enumerable.Empty<RuntimePackageDependency>();
        }

        public bool Equals(RuntimeGraph other)
        {
            // Breaking this up to ease debugging. The optimizer should be able to handle this, so don't refactor unless you have data :).
            if (other == null)
            {
                return false;
            }

            var runtimesEqual = Runtimes
               .OrderBy(pair => pair.Key)
               .SequenceEqual(other.Runtimes.OrderBy(pair => pair.Key));
            var supportsEqual = Supports
               .OrderBy(pair => pair.Key)
               .SequenceEqual(other.Supports.OrderBy(pair => pair.Key));
            return runtimesEqual && supportsEqual;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimeGraph);
        }

        public override int GetHashCode()
        {
            return Runtimes.GetHashCode();
        }
    }
}

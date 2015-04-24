using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.RuntimeModel
{
    public class RuntimeGraph : IEquatable<RuntimeGraph>
    {
        public IDictionary<string, RuntimeDescription> Runtimes { get; }

        public RuntimeGraph()
        {
            Runtimes = new Dictionary<string, RuntimeDescription>();
        }

        public RuntimeGraph(IEnumerable<RuntimeDescription> runtimes)
        {
            Runtimes = runtimes.ToDictionary(r => r.RuntimeIdentifier);
        }

        public RuntimeGraph Clone() => new RuntimeGraph(Runtimes.Values.Select(r => r.Clone()));

        /// <summary>
        /// Merges the content of the other runtime graph in to this runtime graph
        /// </summary>
        /// <param name="other">The other graph to merge in to this graph</param>
        public static RuntimeGraph Merge(RuntimeGraph left, RuntimeGraph right)
        {
            Dictionary<string, RuntimeDescription> runtimes = new Dictionary<string, RuntimeDescription>();
            foreach(var runtime in left.Runtimes.Values)
            {
                runtimes[runtime.RuntimeIdentifier] = runtime.Clone();
            }

            // Merge the right-side runtimes
            foreach(var runtime in right.Runtimes.Values)
            {
                // Check if we already have the runtime defined
                RuntimeDescription leftRuntime;
                if(runtimes.TryGetValue(runtime.RuntimeIdentifier, out leftRuntime))
                {
                    // Merge runtimes
                    runtimes[runtime.RuntimeIdentifier] = RuntimeDescription.Merge(leftRuntime, runtime);
                }
                else
                {
                    runtimes[runtime.RuntimeIdentifier] = runtime;
                }
            }

            return new RuntimeGraph(runtimes.Values);
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
            for(int i = 0; i < expansions.Count; i++)
            {
                // expansions.Count will keep growing as we add items, but thats OK, we want to expand until we stop getting new items
                RuntimeDescription desc;
                if(Runtimes.TryGetValue(expansions[i], out desc))
                {
                    // Add the inherited runtimes to the list
                    foreach(var inheritedRuntime in desc.InheritedRuntimes)
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

        public IEnumerable<RuntimePackageDependency> FindRuntimeDependencies(string runtimeName, string packageId)
        {
            // PERF: We could cache this for a particular (runtimeName,packageId) pair.
            foreach(var expandedRuntime in ExpandRuntime(runtimeName))
            {
                RuntimeDescription runtimeDescription;
                if(Runtimes.TryGetValue(expandedRuntime, out runtimeDescription))
                {
                    RuntimeDependencySet dependencySet;
                    if(runtimeDescription.RuntimeDependencySets.TryGetValue(packageId, out dependencySet))
                    {
                        return dependencySet.Dependencies.Values;
                    }
                }
            }
            return Enumerable.Empty<RuntimePackageDependency>();
        }

        public bool Equals(RuntimeGraph other) => other != null && other.Runtimes
            .OrderBy(pair => pair.Key)
            .SequenceEqual(other.Runtimes.OrderBy(pair => pair.Key));

        public override bool Equals(object obj) => Equals(obj as RuntimeGraph);
        public override int GetHashCode() => Runtimes.GetHashCode();
    }
}

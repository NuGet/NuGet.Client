using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.RuntimeModel
{
    public class RuntimeDescription : IEquatable<RuntimeDescription>
    {
        public string RuntimeIdentifier { get; }
        public IReadOnlyList<string> InheritedRuntimes { get; }
        public IReadOnlyDictionary<string, RuntimeDependencySet> RuntimeDependencySets { get; }

        public RuntimeDescription(string runtimeIdentifier) : this(runtimeIdentifier, Enumerable.Empty<string>(), Enumerable.Empty<RuntimeDependencySet>()) { }
        public RuntimeDescription(string runtimeIdentifier, IEnumerable<string> inheritedRuntimes) : this(runtimeIdentifier, inheritedRuntimes, Enumerable.Empty<RuntimeDependencySet>()) { }
        public RuntimeDescription(string runtimeIdentifier, IEnumerable<RuntimeDependencySet> runtimeDependencySets) : this(runtimeIdentifier, Enumerable.Empty<string>(), runtimeDependencySets) { }

        public RuntimeDescription(string runtimeIdentifier, IEnumerable<string> inheritedRuntimes, IEnumerable<RuntimeDependencySet> runtimeDependencySets)
        {
            RuntimeIdentifier = runtimeIdentifier;
            InheritedRuntimes = inheritedRuntimes.ToList().AsReadOnly();
            RuntimeDependencySets = runtimeDependencySets.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        }

        public bool Equals(RuntimeDescription other) => other != null &&
            string.Equals(other.RuntimeIdentifier, RuntimeIdentifier, StringComparison.Ordinal) &&
            InheritedRuntimes.OrderBy(s => s).SequenceEqual(other.InheritedRuntimes.OrderBy(s => s)) &&
            RuntimeDependencySets.OrderBy(p => p.Key).SequenceEqual(other.RuntimeDependencySets.OrderBy(p => p.Key));

        public RuntimeDescription Clone() => new RuntimeDescription(RuntimeIdentifier, InheritedRuntimes, RuntimeDependencySets.Values.Select(d => d.Clone()));

        /// <summary>
        /// Merges the content of the other runtime description in to this runtime description
        /// </summary>
        /// <param name="other">The other description to merge in to this description</param>
        public static RuntimeDescription Merge(RuntimeDescription left, RuntimeDescription right)
        {
            if(!string.Equals(left.RuntimeIdentifier, right.RuntimeIdentifier, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TODO: Unable to merge runtimes, they do not have the same identifier");
            }

            // Merge #imports
            List<string> inheritedRuntimes;
            if(right.InheritedRuntimes.Count != 0)
            {
                // Ack! Imports in both!
                if (left.InheritedRuntimes.Count != 0)
                {
                    // Can't merge inherited runtimes!
                    throw new InvalidOperationException($"TODO: Cannot merge the '#imports' property of {left.RuntimeIdentifier}. Only one runtime.json should define '#imports' for a particular runtime!");
                }

                // Copy #imports from right
                inheritedRuntimes = new List<string>(right.InheritedRuntimes);
            }
            else
            {
                // Copy #imports from left (if any)
                inheritedRuntimes = new List<string>(left.InheritedRuntimes);
            }

            // Merge dependency sets
            Dictionary<string, RuntimeDependencySet> newSets = new Dictionary<string, RuntimeDependencySet>();
            foreach(var dependencySet in left.RuntimeDependencySets.Values)
            {
                newSets[dependencySet.Id] = dependencySet.Clone();
            }

            // Overwrite with things from the right
            foreach(var dependencySet in right.RuntimeDependencySets.Values)
            {
                newSets[dependencySet.Id] = dependencySet.Clone();
            }

            return new RuntimeDescription(left.RuntimeIdentifier, inheritedRuntimes, newSets.Values);
        }

        public override bool Equals(object obj) => Equals(obj as RuntimeDescription);
        public override int GetHashCode() => HashCodeCombiner.Start()
            .AddObject(RuntimeIdentifier)
            .AddObject(InheritedRuntimes)
            .AddObject(RuntimeDependencySets);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.RuntimeModel
{
    public class RuntimeDescription : IEquatable<RuntimeDescription>
    {
        private List<string> _inheritedRuntimes;
        public string RuntimeIdentifier { get; }
        public IReadOnlyList<string> InheritedRuntimes => _inheritedRuntimes.AsReadOnly();
        public IDictionary<string, RuntimeDependencySet> AdditionalDependencies { get; }

        public RuntimeDescription(string runtimeIdentifier) : this(runtimeIdentifier, Enumerable.Empty<string>(), Enumerable.Empty<RuntimeDependencySet>()) { }
        public RuntimeDescription(string runtimeIdentifier, IEnumerable<string> inheritedRuntimes) : this(runtimeIdentifier, inheritedRuntimes, Enumerable.Empty<RuntimeDependencySet>()) { }
        public RuntimeDescription(string runtimeIdentifier, IEnumerable<RuntimeDependencySet> additionalDependencies) : this(runtimeIdentifier, Enumerable.Empty<string>(), additionalDependencies) { }

        public RuntimeDescription(string runtimeIdentifier, IEnumerable<string> inheritedRuntimes, IEnumerable<RuntimeDependencySet> additionalDependencies)
        {
            RuntimeIdentifier = runtimeIdentifier;
            _inheritedRuntimes = inheritedRuntimes.ToList();
            AdditionalDependencies = additionalDependencies.ToDictionary(d => d.Id);
        }

        public bool Equals(RuntimeDescription other) => other != null &&
            string.Equals(other.RuntimeIdentifier, RuntimeIdentifier, StringComparison.Ordinal) &&
            InheritedRuntimes.OrderBy(s => s).SequenceEqual(other.InheritedRuntimes.OrderBy(s => s)) &&
            AdditionalDependencies.OrderBy(p => p.Key).SequenceEqual(other.AdditionalDependencies.OrderBy(p => p.Key));

        /// <summary>
        /// Merges the content of the other runtime description in to this runtime description
        /// </summary>
        /// <param name="other">The other description to merge in to this description</param>
        public void MergeIn(RuntimeDescription otherRuntime)
        {
            if(!string.Equals(otherRuntime.RuntimeIdentifier, RuntimeIdentifier, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TODO: Unable to merge runtimes, they do not have the same identifier");
            }

            // Merge #imports
            if(otherRuntime.InheritedRuntimes.Count != 0)
            {
                if (InheritedRuntimes.Count != 0)
                {
                    // Can't merge inherited runtimes!
                    throw new InvalidOperationException("TODO: Cannot merge the '#imports' property of a runtime. Only one runtime.json should define '#imports' for a particular runtime!");
                }

                // Copy #imports
                _inheritedRuntimes = new List<string>(otherRuntime.InheritedRuntimes);
            }

            // Merge dependency sets
            foreach(var dependencySet in otherRuntime.AdditionalDependencies)
            {
                RuntimeDependencySet myDependencySet;
                if(AdditionalDependencies.TryGetValue(dependencySet.Key, out myDependencySet))
                {
                    myDependencySet.MergeIn(dependencySet.Value);
                }
                else
                {
                    AdditionalDependencies.Add(dependencySet.Key, dependencySet.Value);
                }
            }
        }

        public override bool Equals(object obj) => Equals(obj as RuntimeDescription);
        public override int GetHashCode() => HashCodeCombiner.Start()
            .AddObject(RuntimeIdentifier)
            .AddObject(InheritedRuntimes)
            .AddObject(AdditionalDependencies);
    }
}

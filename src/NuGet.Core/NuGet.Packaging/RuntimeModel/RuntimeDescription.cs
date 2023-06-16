// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Shared;

namespace NuGet.RuntimeModel
{
    public class RuntimeDescription : IEquatable<RuntimeDescription>
    {
        private static readonly IReadOnlyDictionary<string, RuntimeDependencySet> EmptyRuntimeDependencySets = new Dictionary<string, RuntimeDependencySet>();

        public string RuntimeIdentifier { get; }
        public IReadOnlyList<string> InheritedRuntimes { get; }

        /// <summary>
        /// RID specific package dependencies.
        /// </summary>
        public IReadOnlyDictionary<string, RuntimeDependencySet> RuntimeDependencySets { get; }

        public RuntimeDescription(string runtimeIdentifier)
            : this(runtimeIdentifier, null, null)
        {
        }

        public RuntimeDescription(string runtimeIdentifier, IEnumerable<string> inheritedRuntimes)
            : this(runtimeIdentifier, inheritedRuntimes, null)
        {
        }

        public RuntimeDescription(string runtimeIdentifier, IEnumerable<RuntimeDependencySet> runtimeDependencySets)
            : this(runtimeIdentifier, null, runtimeDependencySets)
        {
        }

        public RuntimeDescription(string runtimeIdentifier, IEnumerable<string> inheritedRuntimes, IEnumerable<RuntimeDependencySet> runtimeDependencySets)
            : this(
                runtimeIdentifier,
                inheritedRuntimes?.ToList(),
                runtimeDependencySets?.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase))
        {
        }

        private RuntimeDescription(string runtimeIdentifier, IReadOnlyList<string> inheritedRuntimes, IReadOnlyDictionary<string, RuntimeDependencySet> runtimeDependencySets)
        {
            RuntimeIdentifier = runtimeIdentifier;
            InheritedRuntimes = inheritedRuntimes is null or { Count: 0 } ? Array.Empty<string>() : inheritedRuntimes;
            RuntimeDependencySets = runtimeDependencySets is null or { Count: 0 } ? EmptyRuntimeDependencySets : runtimeDependencySets;
        }

        public bool Equals(RuntimeDescription other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return string.Equals(other.RuntimeIdentifier, RuntimeIdentifier, StringComparison.Ordinal)
                && InheritedRuntimes.OrderedEquals(other.InheritedRuntimes, s => s, StringComparer.Ordinal, StringComparer.Ordinal)
                && RuntimeDependencySets.OrderedEquals(other.RuntimeDependencySets, p => p.Key, StringComparer.OrdinalIgnoreCase);
        }

        public RuntimeDescription Clone()
        {
            return new RuntimeDescription(
                RuntimeIdentifier,
                InheritedRuntimes,
                CloneRuntimeDependencySets());

            IReadOnlyDictionary<string, RuntimeDependencySet> CloneRuntimeDependencySets()
            {
                if (RuntimeDependencySets.Count == 0)
                {
                    return EmptyRuntimeDependencySets;
                }

                Dictionary<string, RuntimeDependencySet> clone = new(capacity: RuntimeDependencySets.Count, StringComparer.OrdinalIgnoreCase);

                // No allocations for this enumeration
                foreach (var pair in (Dictionary<string, RuntimeDependencySet>)RuntimeDependencySets)
                {
                    clone[pair.Key] = pair.Value.Clone();
                }

                return clone;
            }
        }

        /// <summary>
        /// Merges the content of the other runtime description in to this runtime description
        /// </summary>
        /// <param name="other">The other description to merge in to this description</param>
        public static RuntimeDescription Merge(RuntimeDescription left, RuntimeDescription right)
        {
            if (!string.Equals(left.RuntimeIdentifier, right.RuntimeIdentifier, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TODO: Unable to merge runtimes, they do not have the same identifier");
            }

            // Merge #imports
            List<string> inheritedRuntimes;
            if (right.InheritedRuntimes.Count != 0 && left.InheritedRuntimes.Count == 0)
            {
                // Copy #imports from right
                inheritedRuntimes = new List<string>(right.InheritedRuntimes);
            }
            else
            {
                // Ignore the inherited runtimes from the right if there are inherited runtimes on the left.

                // Copy #imports from left (if any)
                inheritedRuntimes = new List<string>(left.InheritedRuntimes);
            }

            // Merge dependency sets
            var newSets = new Dictionary<string, RuntimeDependencySet>();
            foreach (var dependencySet in left.RuntimeDependencySets.Values)
            {
                newSets[dependencySet.Id] = dependencySet.Clone();
            }

            // Overwrite with things from the right
            foreach (var dependencySet in right.RuntimeDependencySets.Values)
            {
                newSets[dependencySet.Id] = dependencySet.Clone();
            }

            return new RuntimeDescription(
                left.RuntimeIdentifier,
                // If collections are empty, pass null to avoid allocations.
                inheritedRuntimes.Count == 0 ? null : inheritedRuntimes,
                newSets.Count == 0 ? null : newSets);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimeDescription);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(RuntimeIdentifier);
            combiner.AddSequence(InheritedRuntimes);
            combiner.AddDictionary(RuntimeDependencySets);

            return combiner.CombinedHash;
        }

        public override string ToString()
        {
            return $"({RuntimeIdentifier}: (#imports: {string.Join(",", InheritedRuntimes)}); {string.Join(",", RuntimeDependencySets)})";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.RuntimeModel
{
    public class RuntimeDependencySet : IEquatable<RuntimeDependencySet>
    {
        public string Id { get; }
        public IDictionary<string, RuntimePackageDependency> Dependencies { get; }

        public RuntimeDependencySet(string id) : this(id, Enumerable.Empty<RuntimePackageDependency>()) { }
        public RuntimeDependencySet(string id, IEnumerable<RuntimePackageDependency> dependencies)
        {
            Id = id;
            Dependencies = dependencies.ToDictionary(d => d.Id);
        }

        public bool Equals(RuntimeDependencySet other) => other != null &&
            string.Equals(other.Id, Id, StringComparison.Ordinal) &&
            Dependencies.OrderBy(p => p.Key).SequenceEqual(other.Dependencies.OrderBy(p => p.Key));

        public override bool Equals(object obj) => Equals(obj as RuntimeDependencySet);

        public override int GetHashCode() => HashCodeCombiner.Start()
            .AddObject(Id)
            .AddObject(Dependencies);

        public void MergeIn(RuntimeDependencySet dependencySet)
        {
            if(!string.Equals(dependencySet.Id, Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TODO: Unable to merge dependency sets, they do not have the same package id");
            }

            // Merge dependencies!
            foreach(var dependency in dependencySet.Dependencies.Values)
            {
                // REVIEW: Overwrite dependencies?
                if(Dependencies.ContainsKey(dependency.Id))
                {
                    throw new InvalidOperationException("TODO: Duplicate runtime dependencies defined for " + dependency.Id);
                }
                Dependencies.Add(dependency.Id, dependency);
            }
        }
    }
}

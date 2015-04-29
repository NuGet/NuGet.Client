using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.RuntimeModel
{
    public class RuntimeDependencySet : IEquatable<RuntimeDependencySet>
    {
        public string Id { get; }
        public IReadOnlyDictionary<string, RuntimePackageDependency> Dependencies { get; }

        public RuntimeDependencySet(string id) : this(id, Enumerable.Empty<RuntimePackageDependency>()) { }
        public RuntimeDependencySet(string id, IEnumerable<RuntimePackageDependency> dependencies)
        {
            Id = id;
            Dependencies = new ReadOnlyDictionary<string, RuntimePackageDependency>(dependencies.ToDictionary(d => d.Id));
        }

        public bool Equals(RuntimeDependencySet other)
        {
            return other != null &&
                string.Equals(other.Id, Id, StringComparison.Ordinal) &&
                Dependencies.OrderBy(p => p.Key).SequenceEqual(other.Dependencies.OrderBy(p => p.Key));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimeDependencySet);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .AddObject(Id)
                .AddObject(Dependencies);
        }

        public RuntimeDependencySet Clone()
        {
            return new RuntimeDependencySet(Id, Dependencies.Values.Select(d => d.Clone()));
        }
    }
}

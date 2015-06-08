// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NuGet.Common;

namespace NuGet.RuntimeModel
{
    public class RuntimeDependencySet : IEquatable<RuntimeDependencySet>
    {
        public string Id { get; }
        public IReadOnlyDictionary<string, RuntimePackageDependency> Dependencies { get; }

        public RuntimeDependencySet(string id)
            : this(id, Enumerable.Empty<RuntimePackageDependency>())
        {
        }

        public RuntimeDependencySet(string id, IEnumerable<RuntimePackageDependency> dependencies)
        {
            Id = id;
            Dependencies = new ReadOnlyDictionary<string, RuntimePackageDependency>(dependencies.ToDictionary(d => d.Id));
        }

        public bool Equals(RuntimeDependencySet other)
        {
            // Breaking this up to ease debugging. The optimizer should be able to handle this, so don't refactor unless you have data :).
            if (other == null)
            {
                return false;
            }

            var dependenciesEqual = Dependencies
                .OrderBy(p => p.Key)
                .SequenceEqual(other.Dependencies.OrderBy(p => p.Key));

            return string.Equals(other.Id, Id, StringComparison.Ordinal) &&
                   dependenciesEqual;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimeDependencySet);
        }

        public override int GetHashCode()
        {
            return new HashCodeCombiner()
                .AddObject(Id)
                .AddObject(Dependencies)
                .CombinedHash;
        }

        public RuntimeDependencySet Clone()
        {
            return new RuntimeDependencySet(Id, Dependencies.Values.Select(d => d.Clone()));
        }

        public override string ToString()
        {
            return $"{Id} -> {string.Join(",", Dependencies.Select(d => d.Value.Id + " " + d.Value.VersionRange.ToString()))}";
        }
    }
}

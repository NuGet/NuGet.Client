// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class ProjectCentralTransitiveDependencyGroup : IEquatable<ProjectCentralTransitiveDependencyGroup>
    {
        public ProjectCentralTransitiveDependencyGroup(NuGetFramework framework, IEnumerable<LibraryDependency> transitiveDependencies)
        {
            FrameworkName = framework.GetShortFolderName();
            TransitiveDependencies = transitiveDependencies;
        }

        public string FrameworkName { get; }

        public IEnumerable<LibraryDependency> TransitiveDependencies { get; }

        public bool Equals(ProjectCentralTransitiveDependencyGroup other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (string.Equals(FrameworkName, other.FrameworkName, StringComparison.OrdinalIgnoreCase))
            {
                if (TransitiveDependencies == null || other.TransitiveDependencies == null)
                {
                    return TransitiveDependencies == other.TransitiveDependencies;
                }

                return TransitiveDependencies.SequenceEqual(other.TransitiveDependencies);
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectCentralTransitiveDependencyGroup);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.AddStringIgnoreCase(FrameworkName);

            if (TransitiveDependencies != null)
            {
                foreach (var dependency in TransitiveDependencies)
                {
                    combiner.AddObject(dependency);
                }
            }
            return combiner.CombinedHash;
        }
    }
}

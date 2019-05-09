// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class TargetFrameworkInformation : IEquatable<TargetFrameworkInformation>
    {
        public NuGetFramework FrameworkName { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; } = new List<LibraryDependency>();

        /// <summary>
        /// A fallback PCL framework to use when no compatible items
        /// were found for <see cref="FrameworkName"/>.
        /// </summary>
        public IList<NuGetFramework> Imports { get; set; } = new List<NuGetFramework>();

        /// <summary>
        /// If True AssetTargetFallback behavior will be used for Imports.
        /// </summary>
        public bool AssetTargetFallback { get; set; }

        /// <summary>
        /// Display warnings when the Imports framework is used.
        /// </summary>
        public bool Warn { get; set; }

        /// <summary>
        /// List of dependencies that are not part of the graph resolution.
        /// </summary>
        public IList<DownloadDependency> DownloadDependencies { get; } = new List<DownloadDependency>();

        /// <summary>
        /// A set of unique FrameworkReferences
        /// </summary>
        public ISet<FrameworkDependency> FrameworkReferences { get; } = new HashSet<FrameworkDependency>();

        public override string ToString()
        {
            return FrameworkName.GetShortFolderName();
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(FrameworkName);
            hashCode.AddObject(AssetTargetFallback);
            hashCode.AddSequence(Dependencies);
            hashCode.AddSequence(Imports);
            hashCode.AddSequence(DownloadDependencies);
            hashCode.AddSequence(FrameworkReferences);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TargetFrameworkInformation);
        }

        public bool Equals(TargetFrameworkInformation other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityUtility.EqualsWithNullCheck(FrameworkName, other.FrameworkName) &&
                   Dependencies.OrderedEquals(other.Dependencies, dependency => dependency.Name, StringComparer.OrdinalIgnoreCase) &&
                   Imports.SequenceEqualWithNullCheck(other.Imports) &&
                   AssetTargetFallback == other.AssetTargetFallback &&
                   DownloadDependencies.OrderedEquals(other.DownloadDependencies, dep => dep) &&
                   FrameworkReferences.OrderedEquals(other.FrameworkReferences, fr => fr);
        }

        public TargetFrameworkInformation Clone()
        {
            var clonedObject = new TargetFrameworkInformation();
            clonedObject.FrameworkName = FrameworkName;
            clonedObject.Dependencies = Dependencies.Select(item => item.Clone()).ToList();
            clonedObject.Imports = new List<NuGetFramework>(Imports);
            clonedObject.AssetTargetFallback = AssetTargetFallback;
            clonedObject.Warn = Warn;
            clonedObject.DownloadDependencies.AddRange(DownloadDependencies.Select(item => item.Clone()));
            clonedObject.FrameworkReferences.AddRange(FrameworkReferences);
            return clonedObject;
        }
    }
}

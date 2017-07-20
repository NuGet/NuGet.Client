// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class TargetFrameworkInformation : IEquatable<TargetFrameworkInformation>
    {
        public NuGetFramework FrameworkName { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; } = new List<LibraryDependency>();

        /// <summary>
        /// A fallback PCL framework to use when no compatible items
        /// were found for <see cref="FrameworkName"/>. This check is done
        /// per asset type.
        /// </summary>
        /// <remarks>Deprecated. Use <see cref="AssetTargetFallback" /> instead.</remarks>
        /// <see cref="Imports" /> cannot be used with <see cref="AssetTargetFallback" />.
        public IList<NuGetFramework> Imports { get; set; } = new List<NuGetFramework>();

        /// <summary>
        /// A fallback framework to use when no compatible items
        /// were found for <see cref="FrameworkName"/>. 
        /// <see cref="AssetTargetFallback" /> will only fallback if the package
        /// does not contain any assets compatible with <see cref="FrameworkName"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="AssetTargetFallback" /> cannot be used with <see cref="Imports" />.
        /// </remarks>
        public IList<NuGetFramework> AssetTargetFallback { get; set; } = new List<NuGetFramework>();

        /// <summary>
        /// Display warnings when the Imports framework is used.
        /// </summary>
        public bool Warn { get; set; }

        public override string ToString()
        {
            return FrameworkName.GetShortFolderName();
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(FrameworkName);
            hashCode.AddSequence(Dependencies);

            // Add markers between NuGetFramework objects
            hashCode.AddObject(nameof(Imports));
            hashCode.AddSequence(Imports);

            hashCode.AddObject(nameof(AssetTargetFallback));
            hashCode.AddSequence(AssetTargetFallback);

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
                   Dependencies.SequenceEqualWithNullCheck(other.Dependencies) &&
                   Imports.SequenceEqualWithNullCheck(other.Imports) &&
                   AssetTargetFallback.SequenceEqualWithNullCheck(other.AssetTargetFallback);
        }
    }
}

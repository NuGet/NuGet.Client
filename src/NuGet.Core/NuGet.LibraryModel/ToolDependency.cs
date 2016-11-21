// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.LibraryModel
{
    public class ToolDependency : IEquatable<ToolDependency>
    {
        public LibraryRange LibraryRange { get; set; }
        public List<NuGetFramework> Imports { get; set; }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(LibraryRange);
            hashCode.AddSequence(Imports);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ToolDependency);
        }

        public bool Equals(ToolDependency other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityUtility.EqualsWithNullCheck(LibraryRange, other.LibraryRange) &&
                   Imports.SequenceEqualWithNullCheck(other.Imports, new NuGetFrameworkFullComparer());
        }
    }
}
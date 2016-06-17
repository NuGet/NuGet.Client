﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using NuGet.Versioning;

namespace NuGet.LibraryModel
{
    public class LibraryRange : IEquatable<LibraryRange>
    {
        public LibraryRange()
        {
        }

        public LibraryRange(string name, LibraryDependencyTarget typeConstraint): this(name, null, typeConstraint)
        {
        }

        public LibraryRange(string name, VersionRange versionRange, LibraryDependencyTarget typeConstraint)
        {
            Name = name;
            VersionRange = versionRange;
            TypeConstraint = typeConstraint;
        }

        public string Name { get; set; }

        public VersionRange VersionRange { get; set; }

        public LibraryDependencyTarget TypeConstraint { get; set; } = LibraryDependencyTarget.All;

        public override string ToString()
        {
            var output = Name;
            if (VersionRange != null)
            {
                output = $"{output} {VersionRange.ToNonSnapshotRange().PrettyPrint()}";
            }

            // Append the type constraint in a user friendly way if one exists
            var contraintString = string.Empty;

            switch (TypeConstraint)
            {
                case LibraryDependencyTarget.Reference:
                    contraintString = LibraryType.Reference;
                    break;
                case LibraryDependencyTarget.ExternalProject:
                    contraintString = LibraryType.ExternalProject;
                    break;
                case LibraryDependencyTarget.Project:
                case LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject:
                    contraintString = LibraryType.Project;
                    break;
            }

            if (!string.IsNullOrEmpty(contraintString))
            {
                output = $"{contraintString}/{output}";
            }

            return output;
        }

        public string ToLockFileDependencyGroupString()
        {
            var sb = new StringBuilder();
            sb.Append(Name);

            if (VersionRange != null)
            {
                if (VersionRange.HasLowerBound)
                {
                    sb.Append(" ");

                    if (VersionRange.IsMinInclusive)
                    {
                        sb.Append(">= ");
                    }
                    else
                    {
                        sb.Append("> ");
                    }

                    if (VersionRange.IsFloating)
                    {
                        sb.Append(VersionRange.Float.ToString());
                    }
                    else
                    {
                        sb.Append(VersionRange.MinVersion.ToNormalizedString());
                    }
                }

                if (VersionRange.HasUpperBound)
                {
                    sb.Append(" ");

                    sb.Append(VersionRange.IsMaxInclusive ? "<= " : "< ");
                    sb.Append(VersionRange.MaxVersion.ToNormalizedString());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// True if the type constraint allows the flag.
        /// </summary>
        public bool TypeConstraintAllows(LibraryDependencyTarget flag)
        {
            return (TypeConstraint & flag) == flag;
        }

        /// <summary>
        /// True if any flags are allowed by the constraint.
        /// </summary>
        public bool TypeConstraintAllowsAnyOf(LibraryDependencyTarget flag)
        {
            return (TypeConstraint & flag) != LibraryDependencyTarget.None;
        }

        public bool Equals(LibraryRange other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return TypeConstraint == other.TypeConstraint
                && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                && Equals(VersionRange, other.VersionRange);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LibraryRange);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0) * 397) ^
                       (VersionRange != null ? VersionRange.GetHashCode() : 0) ^
                       TypeConstraint.GetHashCode();
            }
        }

        public static bool operator ==(LibraryRange left, LibraryRange right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LibraryRange left, LibraryRange right)
        {
            return !Equals(left, right);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using NuGet.Versioning;

namespace NuGet.LibraryModel
{
    public class LibraryRange : IEquatable<LibraryRange>
    {
        public string Name { get; set; }

        public VersionRange VersionRange { get; set; }

        public LibraryTypeFlag TypeConstraint { get; set; } = LibraryTypeFlag.All;

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
                case LibraryTypeFlag.Reference:
                    contraintString = LibraryTypes.Reference;
                    break;
                case LibraryTypeFlag.ExternalProject:
                    contraintString = LibraryTypes.ExternalProject;
                    break;
                case LibraryTypeFlag.Project:
                case LibraryTypeFlag.Project | LibraryTypeFlag.ExternalProject:
                    contraintString = LibraryTypes.Project;
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
            sb.Append(" ");

            if (VersionRange == null)
            {
                return sb.ToString();
            }

            var minVersion = VersionRange.MinVersion;
            var maxVersion = VersionRange.MaxVersion;

            sb.Append(">= ");

            if (VersionRange.IsFloating)
            {
                sb.Append(VersionRange.Float.ToString());
            }
            else
            {
                sb.Append(minVersion.ToString());
            }

            if (maxVersion != null)
            {
                sb.Append(VersionRange.IsMaxInclusive ? "<= " : "< ");
                sb.Append(maxVersion.Version.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// True if the type constraint allows the flag.
        /// </summary>
        public bool TypeConstraintAllows(LibraryTypeFlag flag)
        {
            return (TypeConstraint & flag) == flag;
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
                && string.Equals(Name, other.Name)
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
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.LibraryModel
{
    public class LibraryIdentity : IEquatable<LibraryIdentity>, IComparable<LibraryIdentity>
    {
        public LibraryIdentity()
        {
        }

        public LibraryIdentity(string name, NuGetVersion version, LibraryType type)
        {
            Name = name;
            Version = version;
            Type = type;
        }

        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public LibraryType Type { get; set; }

        public override string ToString()
        {
            return Type + "/" + Name + " " + Version;
        }

        public bool Equals(LibraryIdentity other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(Name, other.Name) &&
                   Equals(Version, other.Version) &&
                   Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LibraryIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^
                       (Version != null ? Version.GetHashCode() : 0) ^
                       (Type != null ? Type.GetHashCode() : 0);
            }
        }

        public static bool operator ==(LibraryIdentity left, LibraryIdentity right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LibraryIdentity left, LibraryIdentity right)
        {
            return !Equals(left, right);
        }

        public static implicit operator LibraryRange(LibraryIdentity library)
        {
            return new LibraryRange
            {
                Name = library.Name,
                TypeConstraint = LibraryDependencyTargetUtils.Parse(library.Type),
                VersionRange = library.Version == null ? null
                        : new VersionRange(
                            library.Version,
                            new FloatRange(NuGetVersionFloatBehavior.None, library.Version))
            };
        }

        public int CompareTo(LibraryIdentity other)
        {
            var compare = string.Compare(Type, other.Type, StringComparison.OrdinalIgnoreCase);
            if (compare != 0)
            {
                return compare;
            }

            compare = string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            if (compare == 0)
            {
                if (Version == null
                    && other.Version == null)
                {
                    // NOOP;
                }
                else if (Version == null)
                {
                    compare = -1;
                }
                else if (other.Version == null)
                {
                    compare = 1;
                }
                else
                {
                    compare = Version.CompareTo(other.Version);
                }
            }
            return compare;
        }
    }
}

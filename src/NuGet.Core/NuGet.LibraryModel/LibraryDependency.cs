// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using NuGet.Shared;

namespace NuGet.LibraryModel
{
    public class LibraryDependency : IEquatable<LibraryDependency>
    {
        public LibraryRange LibraryRange { get; set; }

        public LibraryDependencyType Type { get; set; } = LibraryDependencyType.Default;

        public LibraryIncludeFlags IncludeType { get; set; } = LibraryIncludeFlags.All;

        public LibraryIncludeFlags SuppressParent { get; set; } = LibraryIncludeFlagUtils.DefaultSuppressParent;

        public string Name
        {
            get { return LibraryRange.Name; }
        }

        /// <summary>
        /// True if the PackageReference is added by the SDK and not the user.
        /// </summary>
        public bool AutoReferenced { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(LibraryRange);
            sb.Append(" ");
            sb.Append(Type);
            sb.Append(" ");
            sb.Append(LibraryIncludeFlagUtils.GetFlagString(IncludeType));
            return sb.ToString();
        }

        /// <summary>
        /// Type property flag
        /// </summary>
        public bool HasFlag(LibraryDependencyTypeFlag flag)
        {
            return Type.Contains(flag);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(LibraryRange);
            hashCode.AddObject(Type);
            hashCode.AddObject(IncludeType);
            hashCode.AddObject(SuppressParent);
            hashCode.AddObject(AutoReferenced);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LibraryDependency);
        }

        public bool Equals(LibraryDependency other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return AutoReferenced == other.AutoReferenced &&
                   EqualityUtility.EqualsWithNullCheck(LibraryRange, other.LibraryRange) &&
                   EqualityUtility.EqualsWithNullCheck(Type, other.Type) &&
                   IncludeType == other.IncludeType &&
                   SuppressParent == other.SuppressParent;
        }

        public LibraryDependency Clone()
        {
            return new LibraryDependency
            {
                IncludeType = IncludeType,
                LibraryRange = new LibraryRange
                {
                    Name = LibraryRange.Name,
                    TypeConstraint = LibraryRange.TypeConstraint,
                    VersionRange = LibraryRange.VersionRange
                },
                SuppressParent = SuppressParent,
                Type = Type,
                AutoReferenced = AutoReferenced
            };
        }
    }
}

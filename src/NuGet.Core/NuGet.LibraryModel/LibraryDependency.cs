// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.LibraryModel
{
    public class LibraryDependency : IEquatable<LibraryDependency>
    {
        public LibraryRange LibraryRange { get; set; }

        public LibraryDependencyType Type { get; set; } = LibraryDependencyType.Default;

        public LibraryIncludeFlags IncludeType { get; set; } = LibraryIncludeFlags.All;

        public LibraryIncludeFlags SuppressParent { get; set; } = LibraryIncludeFlagUtils.DefaultSuppressParent;

        public IList<NuGetLogCode> NoWarn { get; set; } = new List<NuGetLogCode>();

        public string Name => LibraryRange.Name;

        /// <summary>
        /// True if the PackageReference is added by the SDK and not the user.
        /// </summary>
        public bool AutoReferenced { get; set; }

        /// <summary>
        /// True if the dependency has the version set through CentralPackagVersionManagement file.
        /// </summary>
        public bool VersionCentrallyManaged { get; set; }

        /// <summary>
        /// Information regarding if the dependency is direct or transitive.  
        /// </summary>
        public LibraryDependencyReferenceType ReferenceType { get; set; } = LibraryDependencyReferenceType.Direct;

        public bool GeneratePathProperty { get; set; }

        public string Aliases { get; set; }

        public LibraryDependency() { }

        [Obsolete]
        public LibraryDependency(
            LibraryRange libraryRange,
            LibraryDependencyType type,
            LibraryIncludeFlags includeType,
            LibraryIncludeFlags suppressParent,
            IList<NuGetLogCode> noWarn,
            bool autoReferenced,
            bool generatePathProperty)
            : this(libraryRange, type, includeType, suppressParent, noWarn, autoReferenced, generatePathProperty, versionCentrallyManaged: false, libraryDependencyReferenceType: LibraryDependencyReferenceType.Direct, aliases: null)
        {
        }

        [Obsolete]
        public LibraryDependency(
            LibraryRange libraryRange,
            LibraryDependencyType type,
            LibraryIncludeFlags includeType,
            LibraryIncludeFlags suppressParent,
            IList<NuGetLogCode> noWarn,
            bool autoReferenced,
            bool generatePathProperty,
            bool versionCentrallyManaged,
            LibraryDependencyReferenceType libraryDependencyReferenceType)
            : this(libraryRange, type, includeType, suppressParent, noWarn, autoReferenced, generatePathProperty, versionCentrallyManaged, libraryDependencyReferenceType, aliases: null)
        {
        }

        internal LibraryDependency(
            LibraryRange libraryRange,
            LibraryDependencyType type,
            LibraryIncludeFlags includeType,
            LibraryIncludeFlags suppressParent,
            IList<NuGetLogCode> noWarn,
            bool autoReferenced,
            bool generatePathProperty,
            bool versionCentrallyManaged,
            LibraryDependencyReferenceType libraryDependencyReferenceType,
            string aliases)
        {
            LibraryRange = libraryRange;
            Type = type;
            IncludeType = includeType;
            SuppressParent = suppressParent;
            NoWarn = noWarn;
            AutoReferenced = autoReferenced;
            GeneratePathProperty = generatePathProperty;
            VersionCentrallyManaged = versionCentrallyManaged;
            ReferenceType = libraryDependencyReferenceType;
            Aliases = aliases;
        }

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
            hashCode.AddSequence(NoWarn);
            hashCode.AddObject(GeneratePathProperty);
            hashCode.AddObject(VersionCentrallyManaged);
            hashCode.AddSequence(Aliases);
            hashCode.AddObject(ReferenceType);

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
                   SuppressParent == other.SuppressParent &&
                   NoWarn.SequenceEqualWithNullCheck(other.NoWarn) &&
                   GeneratePathProperty == other.GeneratePathProperty &&
                   VersionCentrallyManaged == other.VersionCentrallyManaged &&
                   Aliases == other.Aliases &&
                   ReferenceType == other.ReferenceType;
        }

        public LibraryDependency Clone()
        {
            var clonedLibraryRange = new LibraryRange(LibraryRange.Name, LibraryRange.VersionRange, LibraryRange.TypeConstraint);
            var clonedNoWarn = new List<NuGetLogCode>(NoWarn);

            return new LibraryDependency(clonedLibraryRange, Type, IncludeType, SuppressParent, clonedNoWarn, AutoReferenced, GeneratePathProperty, VersionCentrallyManaged, ReferenceType, Aliases);
        }

        /// <summary>
        /// Merge the CentralVersion information to the package reference information.
        /// </summary>
        public static void ApplyCentralVersionInformation(IList<LibraryDependency> packageReferences, IDictionary<string, CentralPackageVersion> centralPackageVersions)
        {
            if (packageReferences == null)
            {
                throw new ArgumentNullException(nameof(packageReferences));
            }
            if (centralPackageVersions == null)
            {
                throw new ArgumentNullException(nameof(centralPackageVersions));
            }
            if (centralPackageVersions.Count > 0)
            {
                foreach (LibraryDependency d in packageReferences.Where(d => !d.AutoReferenced && d.LibraryRange.VersionRange == null))
                {
                    if (centralPackageVersions.TryGetValue(d.Name, out CentralPackageVersion centralPackageVersion))
                    {
                        d.LibraryRange.VersionRange = centralPackageVersion.VersionRange;
                    }

                    d.VersionCentrallyManaged = true;
                }
            }
        }
    }
}

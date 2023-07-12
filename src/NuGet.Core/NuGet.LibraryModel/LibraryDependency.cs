// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NuGet.Common;
using NuGet.Shared;
using NuGet.Versioning;

#nullable enable

namespace NuGet.LibraryModel
{
    public class LibraryDependency : IEquatable<LibraryDependency>
    {
        // The size of this class has been reduced by packing six properties into a single 32-bit field.
        // This packing is invisible to external consumers of this type.
        //
        // The packed fields are:
        //
        // Name                      CLR Type                         Unpacked size   Needed size   Packed size
        // ----------------------------------------------------------------------------------------------------
        // GeneratePathProperty      bool                             1 byte          1 bit         1 bit
        // AutoReferenced            bool                             1 byte          1 bit         1 bit
        // VersionCentrallyManaged   bool                             1 byte          1 bit         1 bit
        // IncludeType               LibraryIncludeFlags              2 bytes         7 bits        10 bits
        // SuppressParent            LibraryIncludeFlags              2 bytes         7 bits        10 bits
        // ReferenceType             LibraryDependencyReferenceType   4 bytes         2 bits        6 bits
        //
        // This totals 12 bytes, with padding. It's possible to pack these values into 4 bytes (32 bits),
        // saving 8 bytes per instance of this class.
        //
        // Each enum is given slightly more size than needed, to make any future expansion of the enum
        // less likely to require changes in this class.
        //
        // See comments within each property for packing offsets.

        // Default values for the three packed enum values.
        private const LibraryIncludeFlags DefaultIncludeType = LibraryIncludeFlags.All;
        private const LibraryIncludeFlags DefaultSuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParentConst;
        private const LibraryDependencyReferenceType DefaultReferenceType = LibraryDependencyReferenceType.Direct;

        // Computes, at compile time, the default value for the properties of this class that are packed
        // into the _flags field. Note that all bool values default to false, so we only need to consider
        // the enum values here.
        private const int InitialState =
            ((int)DefaultIncludeType << 3) |
            ((int)DefaultSuppressParent << 13) |
            ((int)DefaultReferenceType << 23);

        private const int Mask10Bits = 0b11_1111_1111;
        private const int Mask6Bits = 0b11_1111;

        /// <summary>
        /// Holds the packed state of six different properties from this class.
        /// </summary>
        private int _flags = InitialState;

        /// <summary>
        /// Lazily allocated backing collection for the <see cref="NoWarn"/> property.
        /// </summary>
        private IList<NuGetLogCode>? _noWarn;

        public bool GeneratePathProperty
        {
            // This property is stored in the lowest bit (0b1) in position 00.
            get => (_flags & 0b1) != 0;
            set => _flags = value ? (_flags | 0b1) : (_flags & ~0b1);
        }

        /// <summary>
        /// True if the PackageReference is added by the SDK and not the user.
        /// </summary>
        public bool AutoReferenced
        {
            // This property is stored in the second lowest bit (0b10) in position 01.
            get => (_flags & 0b10) != 0;
            set => _flags = value ? (_flags | 0b10) : (_flags & ~0b10);
        }

        /// <summary>
        /// True if the dependency has the version set through CentralPackageVersionManagement file.
        /// </summary>
        public bool VersionCentrallyManaged
        {
            // This property is stored in the third lowest bit (0b100) in position 02.
            get => (_flags & 0b100) != 0;
            set => _flags = value ? (_flags | 0b100) : (_flags & ~0b100);
        }

        public LibraryIncludeFlags IncludeType
        {
            // This property is stored in 10 bits (0b1111111111_000), in positions 03 to 12.
            get => (LibraryIncludeFlags)((_flags >> 3) & Mask10Bits);
            set => _flags = (_flags & ~(Mask10Bits << 3)) | ((int)value << 3);
        }

        public LibraryIncludeFlags SuppressParent
        {
            // This property is stored in 10 bits (0b1111111111_0000000000_000), in positions 13 to 22.
            get => (LibraryIncludeFlags)((_flags >> 13) & Mask10Bits);
            set => _flags = (_flags & ~(Mask10Bits << 13)) | ((int)value << 13);
        }

        /// <summary>
        /// Information regarding if the dependency is direct or transitive.  
        /// </summary>
        public LibraryDependencyReferenceType ReferenceType
        {
            // This property is stored in 6 bits (0b111111_0000000000_0000000000_000), in positions 23 to 28.
            get => (LibraryDependencyReferenceType)((_flags >> 23) & Mask6Bits);
            set => _flags = (_flags & ~(Mask6Bits << 23)) | ((int)value << 23);
        }

        /// <summary>
        /// Gets the list of NoWarn codes for this library dependency.
        /// </summary>
        /// <remarks>
        /// This property lazily allocates its backing collection. Callers should check <see cref="NoWarnCount"/>
        /// for a non-zero value before reading this property to avoid redundant allocations.
        /// </remarks>
        [AllowNull]
        public IList<NuGetLogCode> NoWarn
        {
            // Lazily allocate the list if needed.
            get => _noWarn ??= new List<NuGetLogCode>();
            set => _noWarn = value;
        }

        /// <summary>
        /// This internal field will help us avoid allocating a list when calling the count on a null.
        /// </summary>
        public int NoWarnCount => _noWarn?.Count ?? 0;

        public LibraryRange? LibraryRange { get; set; }

        public string Name => LibraryRange!.Name;

        public string? Aliases { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a version override for any centrally defined version.
        /// </summary>
        public VersionRange? VersionOverride { get; set; }

        public LibraryDependency()
        {
        }

        internal LibraryDependency(
            LibraryRange libraryRange,
            LibraryIncludeFlags includeType,
            LibraryIncludeFlags suppressParent,
            IList<NuGetLogCode>? noWarn,
            bool autoReferenced,
            bool generatePathProperty,
            bool versionCentrallyManaged,
            LibraryDependencyReferenceType libraryDependencyReferenceType,
            string? aliases,
            VersionRange? versionOverride)
        {
            LibraryRange = libraryRange;
            IncludeType = includeType;
            SuppressParent = suppressParent;
            _noWarn = noWarn;
            AutoReferenced = autoReferenced;
            GeneratePathProperty = generatePathProperty;
            VersionCentrallyManaged = versionCentrallyManaged;
            ReferenceType = libraryDependencyReferenceType;
            Aliases = aliases;
            VersionOverride = versionOverride;
        }

        private LibraryDependency(
            int flags,
            LibraryRange? libraryRange,
            IList<NuGetLogCode>? noWarn,
            string? aliases,
            VersionRange? versionOverride)
        {
            _flags = flags;
            LibraryRange = libraryRange;
            _noWarn = noWarn;
            Aliases = aliases;
            VersionOverride = versionOverride;
        }

        public override string ToString()
        {
            // Explicitly call .ToString() to ensure string.Concat(string, string, string) overload is called.
            return LibraryRange?.ToString() + " " + LibraryIncludeFlagUtils.GetFlagString(IncludeType);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(_flags);
            hashCode.AddObject(LibraryRange);
            hashCode.AddSequence(_noWarn);
            hashCode.AddObject(Aliases);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as LibraryDependency);
        }

        public bool Equals(LibraryDependency? other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _flags == other._flags &&
                   Aliases == other.Aliases &&
                   EqualityUtility.EqualsWithNullCheck(LibraryRange, other.LibraryRange) &&
                   (_noWarn ?? Enumerable.Empty<NuGetLogCode>()).SequenceEqualWithNullCheck(other._noWarn ?? Enumerable.Empty<NuGetLogCode>()) &&
                   EqualityUtility.EqualsWithNullCheck(VersionOverride, other.VersionOverride);
        }

        public LibraryDependency Clone()
        {
            var clonedLibraryRange = new LibraryRange(LibraryRange!.Name, LibraryRange.VersionRange, LibraryRange.TypeConstraint);
            var clonedNoWarn = _noWarn is null ? null : new List<NuGetLogCode>(_noWarn);

            return new LibraryDependency(_flags, clonedLibraryRange, clonedNoWarn, Aliases, VersionOverride);
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
                foreach (LibraryDependency d in packageReferences.Where(d => !d.AutoReferenced && d.LibraryRange!.VersionRange == null))
                {
                    if (d.VersionOverride != null)
                    {
                        d.LibraryRange!.VersionRange = d.VersionOverride;

                        continue;
                    }

                    if (centralPackageVersions.TryGetValue(d.Name, out CentralPackageVersion centralPackageVersion))
                    {
                        d.LibraryRange!.VersionRange = centralPackageVersion.VersionRange;
                    }

                    d.VersionCentrallyManaged = true;
                }
            }
        }
    }
}

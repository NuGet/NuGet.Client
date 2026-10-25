// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    [DebuggerDisplay("{TargetAlias} / {FrameworkName}")]
    public class TargetFrameworkInformation : IEquatable<TargetFrameworkInformation>
    {
        // private fields to allow for validating initialization values
        private IReadOnlyDictionary<string, CentralPackageVersion> _centralPackageVersions;
        private ImmutableArray<LibraryDependency> _dependencies;
        private ImmutableArray<DownloadDependency> _downloadDependencies;
        private IReadOnlyCollection<FrameworkDependency> _frameworkReferences;
        private ImmutableArray<NuGetFramework> _imports;
        private IReadOnlyDictionary<string, PrunePackageReference> _packagesToPrune;
        private OrderedPackageReferences<CentralPackageVersion> _orderedCentralPackageVersions;
        private OrderedPackageReferences<PrunePackageReference> _orderedPackagesToPrune;

        public string TargetAlias { get; init; } = string.Empty;

        public required NuGetFramework FrameworkName { get; init; }

        public ImmutableArray<LibraryDependency> Dependencies
        {
            get => _dependencies;
            init
            {
                _dependencies = value.IsDefault ? ImmutableArray<LibraryDependency>.Empty : value;
            }
        }

        /// <summary>
        /// A fallback PCL framework to use when no compatible items
        /// were found for <see cref="FrameworkName"/>.
        /// </summary>
        public ImmutableArray<NuGetFramework> Imports
        {
            get => _imports;
            init
            {
                _imports = value.IsDefault ? ImmutableArray<NuGetFramework>.Empty : value;
            }
        }

        /// <summary>
        /// If True AssetTargetFallback behavior will be used for Imports.
        /// </summary>
        public bool AssetTargetFallback { get; init; }

        /// <summary>
        /// Display warnings when the Imports framework is used.
        /// </summary>
        public bool Warn { get; init; }

        /// <summary>
        /// List of dependencies that are not part of the graph resolution.
        /// </summary>
        public ImmutableArray<DownloadDependency> DownloadDependencies
        {
            get => _downloadDependencies;
            init
            {
                _downloadDependencies = value.IsDefault ? ImmutableArray<DownloadDependency>.Empty : value;
            }
        }

        /// <summary>
        /// Package versions defined in the Central package versions management file. 
        /// </summary>
        public IReadOnlyDictionary<string, CentralPackageVersion> CentralPackageVersions
        {
            get => _centralPackageVersions;
            init
            {
                _centralPackageVersions = value ?? ImmutableDictionary<string, CentralPackageVersion>.Empty;
            }
        }

        /// <summary>
        /// A set of unique FrameworkReferences
        /// </summary>
        public IReadOnlyCollection<FrameworkDependency> FrameworkReferences
        {
            get => _frameworkReferences;
            init
            {
                _frameworkReferences = value ?? ImmutableHashSet<FrameworkDependency>.Empty;
            }
        }

        /// <summary>
        /// A dictionary of packages to be pruned from the graph.
        /// An item existing in this dictionary means the pruning capability is enabled.
        /// If pruning is not enabled, this property return Empty regardless of what was specified in the items.
        /// </summary>
        public IReadOnlyDictionary<string, PrunePackageReference> PackagesToPrune
        {
            get => _packagesToPrune;
            init
            {
                _packagesToPrune = value ?? ImmutableDictionary<string, PrunePackageReference>.Empty;
            }
        }

        /// <summary>
        /// The project provided runtime.json
        /// </summary>
        public string RuntimeIdentifierGraphPath { get; init; }

        [SetsRequiredMembers]
        public TargetFrameworkInformation()
        {
            TargetAlias = string.Empty;
            Dependencies = [];
            Imports = [];
            DownloadDependencies = [];
            CentralPackageVersions = ImmutableDictionary<string, CentralPackageVersion>.Empty;
            FrameworkReferences = ImmutableHashSet<FrameworkDependency>.Empty;
            PackagesToPrune = ImmutableDictionary<string, PrunePackageReference>.Empty;
        }

        [SetsRequiredMembers]
        public TargetFrameworkInformation(TargetFrameworkInformation cloneFrom)
        {
            TargetAlias = cloneFrom.TargetAlias;
            FrameworkName = cloneFrom.FrameworkName;
            Dependencies = cloneFrom.Dependencies;
            Imports = cloneFrom.Imports;
            AssetTargetFallback = cloneFrom.AssetTargetFallback;
            Warn = cloneFrom.Warn;
            DownloadDependencies = cloneFrom.DownloadDependencies;
            CentralPackageVersions = cloneFrom.CentralPackageVersions;
            FrameworkReferences = cloneFrom.FrameworkReferences;
            RuntimeIdentifierGraphPath = cloneFrom.RuntimeIdentifierGraphPath;
            PackagesToPrune = cloneFrom.PackagesToPrune;
        }

        internal KeyValuePair<string, CentralPackageVersion>[] GetOrderedCentralPackageVersions()
        {
            return GetOrderedPackageReferences(ref _orderedCentralPackageVersions, CentralPackageVersions, static item => item.Value.Name);
        }

        internal KeyValuePair<string, PrunePackageReference>[] GetOrderedPackagesToPrune()
        {
            return GetOrderedPackageReferences(ref _orderedPackagesToPrune, PackagesToPrune, static item => item.Key);
        }

        private static KeyValuePair<string, T>[] GetOrderedPackageReferences<T>(
            ref OrderedPackageReferences<T> cache,
            IReadOnlyDictionary<string, T> references,
            Func<KeyValuePair<string, T>, string> getName) where T : class
        {
            if (references.Count == 0)
            {
                Volatile.Write(ref cache, null);
                return Array.Empty<KeyValuePair<string, T>>();
            }

            OrderedPackageReferences<T> ordered = Volatile.Read(ref cache);
            if (ordered == null || !ordered.Matches(references))
            {
                ordered = new OrderedPackageReferences<T>(references, getName);
                Volatile.Write(ref cache, ordered);
            }

            return ordered.Items;
        }

        public override string ToString()
        {
            return FrameworkName.GetShortFolderName();
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(FrameworkName);
            hashCode.AddObject(AssetTargetFallback);
            hashCode.AddUnorderedSequence(Dependencies);
            hashCode.AddSequence((IReadOnlyList<NuGetFramework>)Imports);
            hashCode.AddObject(Warn);
            hashCode.AddUnorderedSequence(DownloadDependencies);
            hashCode.AddUnorderedSequence(FrameworkReferences);
            if (RuntimeIdentifierGraphPath != null)
            {
                hashCode.AddObject(PathUtility.GetStringComparerBasedOnOS().GetHashCode(RuntimeIdentifierGraphPath));
            }
            hashCode.AddUnorderedSequence(CentralPackageVersions.Values);
            hashCode.AddUnorderedSequence(PackagesToPrune.Values);
            hashCode.AddStringIgnoreCase(TargetAlias);
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
                   EqualityUtility.OrderedEquals(Dependencies, other.Dependencies, dependency => dependency.Name, StringComparer.OrdinalIgnoreCase) &&
                   Imports.SequenceEqualWithNullCheck(other.Imports) &&
                   Warn == other.Warn &&
                   AssetTargetFallback == other.AssetTargetFallback &&
                   EqualityUtility.OrderedEquals(DownloadDependencies, other.DownloadDependencies, e => e.Name, StringComparer.OrdinalIgnoreCase) &&
                   EqualityUtility.OrderedEquals(FrameworkReferences, other.FrameworkReferences, e => e.Name, ComparisonUtility.FrameworkReferenceNameComparer) &&
                   EqualityUtility.OrderedEquals(CentralPackageVersions.Values, other.CentralPackageVersions.Values, e => e.Name, StringComparer.OrdinalIgnoreCase) &&
                   EqualityUtility.OrderedEquals(PackagesToPrune.Values, other.PackagesToPrune.Values, e => e.Name, StringComparer.OrdinalIgnoreCase) &&
                   PathUtility.GetStringComparerBasedOnOS().Equals(RuntimeIdentifierGraphPath, other.RuntimeIdentifierGraphPath) &&
                   StringComparer.OrdinalIgnoreCase.Equals(TargetAlias, other.TargetAlias);
        }

        private sealed class OrderedPackageReferences<T> where T : class
        {
            private static readonly IComparer<(string Name, int Index)> NameComparer =
                Comparer<(string Name, int Index)>.Create(static (left, right) =>
                {
                    int result = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                    return result != 0 ? result : left.Index.CompareTo(right.Index);
                });

            private readonly int[] _sourceOrder;

            public KeyValuePair<string, T>[] Items { get; }

            public OrderedPackageReferences(
                IReadOnlyDictionary<string, T> references,
                Func<KeyValuePair<string, T>, string> getName)
            {
                Items = references.ToArray();
                _sourceOrder = Array.Empty<int>();
                if (Items.Length > 1)
                {
                    var names = new (string Name, int Index)[Items.Length];
                    for (int i = 0; i < Items.Length; i++)
                    {
                        names[i] = (getName(Items[i]), i);
                    }

                    // Original indices preserve OrderBy's stable ordering when names compare equally.
                    Array.Sort(names, Items, NameComparer);
                    _sourceOrder = new int[Items.Length];
                    for (int i = 0; i < names.Length; i++)
                    {
                        _sourceOrder[names[i].Index] = i;
                    }
                }
            }

            public bool Matches(IReadOnlyDictionary<string, T> references)
            {
                if (references.Count != Items.Length)
                {
                    return false;
                }

                // Read-only dictionaries can wrap mutable collections. Validate source order as well
                // so changes in casing and the stable ordering of case-insensitive ties remain visible.
                int index = 0;
                foreach (KeyValuePair<string, T> item in references)
                {
                    if (index == Items.Length)
                    {
                        return false;
                    }

                    KeyValuePair<string, T> original = Items[_sourceOrder.Length == 0 ? index : _sourceOrder[index]];
                    if (!ReferenceEquals(item.Key, original.Key) || !ReferenceEquals(item.Value, original.Value))
                    {
                        return false;
                    }
                    index++;
                }

                return index == Items.Length;
            }
        }
    }
}

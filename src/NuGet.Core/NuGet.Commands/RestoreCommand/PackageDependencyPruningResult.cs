// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.DependencyResolver;
using NuGet.Shared;

namespace NuGet.Commands
{
    /// <summary>
    /// Positions removed from the package's framework-selected dependency group, in original order.
    /// The package path and selection framework must also participate in the cache identity.
    /// This describes pruning, not dependency-group selection or runtime dependency replacement.
    /// </summary>
    internal readonly struct PackageDependencyPruningResult : IEquatable<PackageDependencyPruningResult>
    {
        private readonly ulong _mask;
        private readonly Overflow? _overflow;

        internal PackageDependencyPruningResult(ulong mask)
        {
            _mask = mask;
            _overflow = null;
        }

        internal PackageDependencyPruningResult(List<int> removed)
        {
            _mask = 0;
            _overflow = removed.Count == 0 ? null : new Overflow(removed);
        }

        public bool Equals(PackageDependencyPruningResult other)
        {
            if (_mask != other._mask)
            {
                return false;
            }
            if (ReferenceEquals(_overflow, other._overflow))
            {
                return true;
            }
            if (_overflow is null || other._overflow is null
                || _overflow.HashCode != other._overflow.HashCode
                || _overflow.Removed.Length != other._overflow.Removed.Length)
            {
                return false;
            }
            for (int i = 0; i < _overflow.Removed.Length; i++)
            {
                if (_overflow.Removed[i] != other._overflow.Removed[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is PackageDependencyPruningResult other && Equals(other);

        public override int GetHashCode() => _overflow?.HashCode ?? _mask.GetHashCode();

        private sealed class Overflow
        {
            internal readonly int[] Removed;
            internal readonly int HashCode;

            internal Overflow(List<int> removed)
            {
                Removed = removed.ToArray();
                var hash = new HashCodeCombiner();
                foreach (int index in Removed)
                {
                    hash.AddObject(index);
                }
                HashCode = hash.CombinedHash;
            }
        }
    }

    internal sealed class PrunedRemoteResolveResult : RemoteResolveResult
    {
        internal required PackageDependencyPruningResult PruningResult { get; init; }
    }
}

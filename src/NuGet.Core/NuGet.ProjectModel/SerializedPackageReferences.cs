// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    internal sealed class SerializedPackageReferences<T>
    {
        internal SerializedPackageReferenceKey<T> Key { get; }
        internal string Json { get; }

        internal SerializedPackageReferences(SerializedPackageReferenceKey<T> key, string json)
        {
            Key = key;
            Json = json ?? throw new ArgumentNullException(nameof(json));
        }
    }

    internal readonly struct SerializedPackageReferenceKey<T>
    {
        internal KeyValuePair<string, T>[] References { get; }
        internal int HashCode { get; }

        internal SerializedPackageReferenceKey(KeyValuePair<string, T>[] references, int hashCode)
        {
            References = references;
            HashCode = hashCode;
        }
    }

    internal sealed class SerializedPackageReferenceComparer<T> : IEqualityComparer<SerializedPackageReferenceKey<T>>
    {
        private readonly Func<KeyValuePair<string, T>, string> _getName;
        private readonly Func<T, VersionRange> _getVersionRange;

        internal SerializedPackageReferenceComparer(
            Func<KeyValuePair<string, T>, string> getName,
            Func<T, VersionRange> getVersionRange)
        {
            _getName = getName;
            _getVersionRange = getVersionRange;
        }

        internal bool TryCreateKey(KeyValuePair<string, T>[] references, out SerializedPackageReferenceKey<T> key)
        {
            var hash = new HashCodeCombiner();
            hash.AddObject(references.Length);
            foreach (var reference in references)
            {
                string? version = _getVersionRange(reference.Value).OriginalString;
                // Generated version text can be overridden by mutable VersionRange subclasses.
                if (version == null)
                {
                    key = default;
                    return false;
                }
                hash.AddObject(_getName(reference), StringComparer.Ordinal);
                hash.AddObject(version, StringComparer.Ordinal);
            }
            key = new SerializedPackageReferenceKey<T>(references, hash.CombinedHash);
            return true;
        }

        public int GetHashCode(SerializedPackageReferenceKey<T> key) => key.HashCode;

        public bool Equals(SerializedPackageReferenceKey<T> left, SerializedPackageReferenceKey<T> right)
        {
            KeyValuePair<string, T>[] first = left.References;
            KeyValuePair<string, T>[] second = right.References;
            if (ReferenceEquals(first, second))
            {
                return true;
            }
            if (first == null || second == null || first.Length != second.Length)
            {
                return false;
            }
            for (int i = 0; i < first.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(_getName(first[i]), _getName(second[i]))
                    || !StringComparer.Ordinal.Equals(
                        _getVersionRange(first[i].Value).OriginalString,
                        _getVersionRange(second[i].Value).OriginalString))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

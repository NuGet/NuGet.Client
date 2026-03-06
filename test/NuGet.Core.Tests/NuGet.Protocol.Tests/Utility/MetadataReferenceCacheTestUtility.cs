// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NuGet.Packaging.Core;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public static class MetadataReferenceCacheTestUtility
    {
        private class ReferenceEqualityComparer<T> : IEqualityComparer, IEqualityComparer<T>
        {
            public bool Equals(T x, T y) => Equals((object)x, y);

            public int GetHashCode(T obj) => GetHashCode((object)obj);

            bool IEqualityComparer.Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        public static void AssertPackagesHaveSameReferences<T>(T first, T second)
        {
            Assert.NotNull(first);
            Assert.NotNull(second);

            // Get all reference-type properties (except Version, which differs between packages).
            var properties =
                typeof(T).GetTypeInfo()
                    .DeclaredProperties.Where(
                        p =>
                            p.Name != nameof(PackageIdentity.Version) &&
                            !p.PropertyType.GetTypeInfo().IsValueType &&
                            p.GetMethod != null);

            // Check that all cached references between the two packages are identical.
            foreach (var property in properties)
            {
                var firstValue = property.GetMethod.Invoke(first, null);
                var secondValue = property.GetMethod.Invoke(second, null);

                if (firstValue != null && secondValue != null)
                {
                    Assert.Same(firstValue, secondValue);
                }
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// Cache strings, dates, and versions to reduce memory.
    /// </summary>
    public class MetadataReferenceCache
    {
        private readonly ConcurrentDictionary<string, string> _stringCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();
        private readonly ConcurrentDictionary<string, NuGetVersion> _versionCache = new ConcurrentDictionary<string, NuGetVersion>(StringComparer.Ordinal);
        private readonly Type _metadataReferenceCacheType = typeof(MetadataReferenceCache);

        /// <summary>
        /// Checks if <paramref name="s"/> already exists in the cache.
        /// If so, returns the cached instance.
        /// If not, caches <paramref name="s"/> and returns it.
        /// </summary>
        public string GetString(string s)
        {
            if (ReferenceEquals(s, null))
            {
                return null;
            }

            if (s.Length == 0)
            {
                return string.Empty;
            }

            return _stringCache.GetOrAdd(s, s);
        }

        /// <summary>
        /// Parses <paramref name="s"/> into a <see cref="NuGetVersion"/>.
        /// </summary>
        public NuGetVersion GetVersion(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return NuGetVersion.Parse(s);
            }

            return _versionCache.GetOrAdd(s, (nugetVersionString) => NuGetVersion.Parse(nugetVersionString));
        }

        /// <summary>
        /// Mapping of input parameter type to caching method.
        /// </summary>
        private static readonly IDictionary<Type, string> CachableTypesMap = new Dictionary<Type, string>
        {
            {typeof(string), nameof(GetString)}
        };

        /// <summary>
        /// <see cref="IEnumerable{Type}"/> containing all types that can be cached.
        /// </summary>
        internal static IEnumerable<Type> CachableTypes => CachableTypesMap.Keys;

        /// <summary>
        /// <see cref="IEnumerable{Type}"/> containing string type methods can be cached.
        /// </summary>
        internal ConcurrentDictionary<Type, MethodInfo> CachableMethodTypes { get; } = new ConcurrentDictionary<Type, MethodInfo>();

        /// <summary>
        /// Iterates through the properties of <paramref name="input"/> that are either <see cref="string"/>s, <see cref="DateTimeOffset"/>s, or <see cref="NuGetVersion"/>s and checks them against the cache.
        /// </summary>
        public T GetObject<T>(T input)
        {
            // Get all properties that contain both a Get method and a Set method and can be cached.
            PropertyInfo[] properties = _propertyCache.GetOrAdd(typeof(T), (typeKeyInput) =>
            {
                return typeKeyInput.GetTypeInfo()
                    .DeclaredProperties.Where(
                        p => CachableTypesMap.ContainsKey(p.PropertyType) && p.GetMethod != null && p.SetMethod != null)
                    .ToArray();
            });

            var stringMethodType = CachableMethodTypes.GetOrAdd(_metadataReferenceCacheType, (metadataReferenceCacheType) =>
            {
                // Doing reflection everytime is expensive so cache it for string type which is all this MetadataReferenceCache about.
                Type stringPropertyType = typeof(string);
                return _metadataReferenceCacheType.GetTypeInfo()
                        .DeclaredMethods.FirstOrDefault(
                            m =>
                                m.Name == CachableTypesMap[stringPropertyType] &&
                                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new Type[] { stringPropertyType }));
            });

            for (var i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                object value = property.GetMethod.Invoke(input, null);

                object cachedValue = property.PropertyType == typeof(string)
                    ? stringMethodType.Invoke(this, new[] { value })
                    : _metadataReferenceCacheType.GetTypeInfo()
                        .DeclaredMethods.FirstOrDefault(
                            m =>
                                m.Name == CachableTypesMap[property.PropertyType] &&
                                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new Type[] { property.PropertyType }))
                        .Invoke(this, new[] { value });
                property.SetMethod.Invoke(input, new[] { cachedValue });
            }

            return input;
        }
    }
}

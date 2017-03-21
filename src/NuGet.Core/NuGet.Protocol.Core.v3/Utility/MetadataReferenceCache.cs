using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NuGet.Versioning;

namespace NuGet.Protocol.Utility
{
    /// <summary>
    /// Cache strings, dates, and versions to reduce memory.
    /// </summary>
    internal class MetadataReferenceCache
    {
        private readonly Dictionary<string, string> _stringCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<DateTimeOffset, DateTimeOffset> _dateCache = new Dictionary<DateTimeOffset, DateTimeOffset>();
        private readonly Dictionary<Version, Version> _systemVersionCache = new Dictionary<Version, Version>();

        // Include metadata in the compare.
        // All catalog versions are normalized so the original string is not a concern.
        private readonly Dictionary<NuGetVersion, NuGetVersion> _versionCache = new Dictionary<NuGetVersion, NuGetVersion>(VersionComparer.VersionReleaseMetadata);

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

            string cachedValue;
            if (!_stringCache.TryGetValue(s, out cachedValue))
            {
                _stringCache.Add(s, s);
                cachedValue = s;
            }

            return cachedValue;
        }

        /// <summary>
        /// Parses <paramref name="s"/> into a <see cref="DateTimeOffset"/>.
        /// If <paramref name="s"/> cannot be parsed into a <see cref="DateTimeOffset"/>, returns null.
        /// Otherwise, checks if the parsed value already exists in the cache.
        /// If so, returns the cached instance.
        /// If not, caches the parsed value and returns it.
        /// </summary>
        public DateTimeOffset? GetDate(string s)
        {
            DateTimeOffset date;
            if (!DateTimeOffset.TryParse(s, out date))
            {
                return null;
            }

            return GetDate(date);
        }

        /// <summary>
        /// Checks if <paramref name="date"/> already exists in the cache.
        /// If so, returns the cached instance.
        /// If not, caches <paramref name="date"/> and returns it.
        /// </summary>
        public DateTimeOffset GetDate(DateTimeOffset date)
        {
            DateTimeOffset cachedValue;
            if (!_dateCache.TryGetValue(date, out cachedValue))
            {
                _dateCache.Add(date, date);
                cachedValue = date;
            }

            return cachedValue;
        }

        /// <summary>
        /// Parses <paramref name="s"/> into a <see cref="NuGetVersion"/>.
        /// If <paramref name="s"/> cannot be parsed into a <see cref="NuGetVersion"/>, returns null.
        /// Otherwise, checks if the parsed value already exists in the cache.
        /// If so, returns the cached instance.
        /// If not, caches the parsed value and returns it.
        /// </summary>
        public NuGetVersion GetVersion(string s)
        {
            NuGetVersion version;
            if (!NuGetVersion.TryParse(s, out version))
            {
                return null;
            }

            return GetVersion(version);
        }

        /// <summary>
        /// Checks if <paramref name="version"/> already exists in the cache.
        /// If so, returns the cached instance.
        /// If not, caches <paramref name="version"/> and returns it.
        /// </summary>
        public NuGetVersion GetVersion(NuGetVersion version)
        {
            NuGetVersion cachedValue;
            if (!_versionCache.TryGetValue(version, out cachedValue))
            {
                var systemVersion = GetSystemVersion(version);

                // Save memory by caching the System.Version and the release label string separately.
                var releaseLabels = version.ReleaseLabels.Select(GetString);

                // Rebuild the version without the original string value.
                version = new NuGetVersion(systemVersion, releaseLabels, GetString(version.Metadata), originalVersion: null);

                _versionCache.Add(version, version);
                cachedValue = version;
            }

            return cachedValue;
        }

        private Version GetSystemVersion(NuGetVersion nugetVersion)
        {
            var version = new Version(nugetVersion.Major, nugetVersion.Minor, nugetVersion.Patch, nugetVersion.Revision);

            Version cachedValue;
            if (!_systemVersionCache.TryGetValue(version, out cachedValue))
            {
                _systemVersionCache.Add(version, version);
                cachedValue = version;
            }

            return cachedValue;
        }

        private readonly IDictionary<Type, string> _cachableTypes = new Dictionary<Type, string>
        {
            {typeof(string), nameof(GetString)},
            {typeof(DateTimeOffset?), nameof(GetDate)},
            {typeof(DateTimeOffset), nameof(GetDate)},
            {typeof(NuGetVersion), nameof(GetVersion)}
        };
        
        /// <summary>
        /// Iterates through the properties of <paramref name="input"/> that are either <see cref="string"/>s, <see cref="DateTimeOffset"/>s, or <see cref="NuGetVersion"/>s and checks them against the cache.
        /// </summary>
        public T GetObject<T>(T input)
        {
            // Get all properties that contain both a Get method and a Set method and can be cached.
            var properties =
                typeof(T).GetTypeInfo()
                    .DeclaredProperties.Where(
                        p => _cachableTypes.ContainsKey(p.PropertyType) && p.GetMethod != null && p.SetMethod != null);

            foreach (var property in properties)
            {
                var value = property.GetMethod.Invoke(input, null);
                var cachedValue =
                    typeof(MetadataReferenceCache).GetTypeInfo()
                        .DeclaredMethods.FirstOrDefault(
                            m =>
                                m.Name == _cachableTypes[property.PropertyType] &&
                                m.GetParameters().Select(p => p.ParameterType) == new Type[] {property.PropertyType})
                        .Invoke(this, new[] {value});
                property.SetMethod.Invoke(input, new[] {cachedValue});
            }

            return input;
        }
    }
}

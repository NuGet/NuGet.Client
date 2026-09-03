// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public static partial class PackagesLockFileFormat
    {
        private static readonly char[] PathSplitChars = ['/'];

        private static PackagesLockFile ReadLockFile(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType == JsonTokenType.None)
            {
                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            int version = int.MinValue;
            IList<TargetReadResult> version1Targets = Array.Empty<TargetReadResult>();
            var version3Targets = new List<KeyValuePair<string, TargetReadResult>>();
            var version3TargetIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = ReadPropertyName(ref reader);
                ReadValue(ref reader);

                if (propertyName == VersionProperty)
                {
                    version = ReadVersion(ref reader);
                }
                else if (propertyName == DependenciesProperty)
                {
                    version1Targets = ReadVersion1TargetsAndVersion3Target(
                        ref reader,
                        out TargetReadResult version3Target);
                    SetValue(version3Targets, version3TargetIndexes, propertyName, version3Target);
                }
                else
                {
                    SetValue(
                        version3Targets,
                        version3TargetIndexes,
                        propertyName,
                        ReadVersion3Target(ref reader, propertyName));
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read())
            {
                throw new JsonException();
            }

            IList<PackagesLockFileTarget> targets = version >= AliasedVersion
                ? GetTargets(version3Targets)
                : GetTargets(version1Targets);

            return new PackagesLockFile
            {
                Version = version,
                Targets = targets
            };
        }

        private static IList<TargetReadResult> ReadVersion1TargetsAndVersion3Target(
            ref Utf8JsonStreamReader reader,
            out TargetReadResult version3Target)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                version3Target = TargetReadResult.Ignored;
                return Array.Empty<TargetReadResult>();
            }

            var version1Targets = new List<KeyValuePair<string, TargetReadResult>>();
            var version1TargetIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            string? framework = null;
            bool frameworkValid = true;
            IList<LockFileDependency> version3Dependencies = Array.Empty<LockFileDependency>();
            bool version3DependenciesValid = true;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = ReadPropertyName(ref reader);
                ReadValue(ref reader);

                if (propertyName == FrameworkProperty)
                {
                    frameworkValid = TryReadScalarAsString(ref reader, out framework);
                    SetValue(
                        version1Targets,
                        version1TargetIndexes,
                        propertyName,
                        CreateVersion1Target(propertyName, Array.Empty<LockFileDependency>()));
                }
                else
                {
                    bool dependenciesValid = TryReadLockFileDependencies(ref reader, out IList<LockFileDependency> dependencies);
                    SetValue(
                        version1Targets,
                        version1TargetIndexes,
                        propertyName,
                        dependenciesValid
                            ? CreateVersion1Target(propertyName, dependencies)
                            : TargetReadResult.Invalid);

                    if (propertyName == DependenciesProperty)
                    {
                        version3Dependencies = dependencies;
                        version3DependenciesValid = dependenciesValid;
                    }
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                version3Target = TargetReadResult.Invalid;
                return new[] { TargetReadResult.Invalid };
            }

            version3Target = CreateVersion3Target(
                DependenciesProperty,
                framework,
                frameworkValid,
                version3Dependencies,
                version3DependenciesValid);

            var targets = new List<TargetReadResult>(version1Targets.Count);
            foreach (KeyValuePair<string, TargetReadResult> target in version1Targets)
            {
                targets.Add(target.Value);
            }

            return targets;
        }

        private static TargetReadResult ReadVersion3Target(
            ref Utf8JsonStreamReader reader,
            string targetName)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return TargetReadResult.Ignored;
            }

            string? framework = null;
            bool frameworkValid = true;
            IList<LockFileDependency> dependencies = Array.Empty<LockFileDependency>();
            bool dependenciesValid = true;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = ReadPropertyName(ref reader);
                ReadValue(ref reader);

                if (propertyName == FrameworkProperty)
                {
                    frameworkValid = TryReadScalarAsString(ref reader, out framework);
                }
                else if (propertyName == DependenciesProperty)
                {
                    dependenciesValid = TryReadLockFileDependencies(ref reader, out dependencies);
                }
                else
                {
                    reader.Skip();
                }
            }

            return reader.TokenType == JsonTokenType.EndObject
                ? CreateVersion3Target(targetName, framework, frameworkValid, dependencies, dependenciesValid)
                : TargetReadResult.Invalid;
        }

        private static TargetReadResult CreateVersion3Target(
            string targetName,
            string? framework,
            bool frameworkValid,
            IList<LockFileDependency> dependencies,
            bool dependenciesValid)
        {
            if (!frameworkValid || !dependenciesValid)
            {
                return TargetReadResult.Invalid;
            }

            if (framework is null || framework.Length == 0)
            {
                return TargetReadResult.Ignored;
            }

            string[] parts = targetName.Split(PathSplitChars);
            return new TargetReadResult(new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse(framework),
                RuntimeIdentifier = parts.Length == 2 ? parts[1] : null,
                TargetAlias = parts[0],
                Dependencies = dependencies
            });
        }

        private static TargetReadResult CreateVersion1Target(
            string targetName,
            IList<LockFileDependency> dependencies)
        {
            string[] parts = targetName.Split(PathSplitChars);
            return new TargetReadResult(new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse(parts[0]),
                RuntimeIdentifier = parts.Length == 2 ? parts[1] : null,
                Dependencies = dependencies
            });
        }

        private static bool TryReadLockFileDependencies(
            ref Utf8JsonStreamReader reader,
            out IList<LockFileDependency> dependencies)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                dependencies = Array.Empty<LockFileDependency>();
                return true;
            }

            var values = new List<KeyValuePair<string, DependencyReadResult>>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string packageId = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                SetValue(values, indexes, packageId, ReadLockFileDependency(ref reader, packageId));
            }

            var result = new List<LockFileDependency>(values.Count);
            foreach (KeyValuePair<string, DependencyReadResult> value in values)
            {
                if (!value.Value.IsValid || value.Value.Dependency is null)
                {
                    dependencies = Array.Empty<LockFileDependency>();
                    return false;
                }

                result.Add(value.Value.Dependency);
            }

            dependencies = result;
            return reader.TokenType == JsonTokenType.EndObject;
        }

        private static DependencyReadResult ReadLockFileDependency(
            ref Utf8JsonStreamReader reader,
            string packageId)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return DependencyReadResult.Invalid;
            }

            string? type = null;
            bool typeValid = true;
            string? resolved = null;
            bool resolvedValid = true;
            string? requested = null;
            bool requestedValid = true;
            string? contentHash = null;
            bool contentHashValid = true;
            IList<PackageDependency> dependencies = Array.Empty<PackageDependency>();
            bool dependenciesValid = true;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = ReadPropertyName(ref reader);
                ReadValue(ref reader);

                switch (propertyName)
                {
                    case TypeProperty:
                        typeValid = TryReadScalarAsString(ref reader, out type);
                        break;
                    case ResolvedProperty:
                        resolvedValid = TryReadScalarAsString(ref reader, out resolved);
                        break;
                    case RequestedProperty:
                        requestedValid = TryReadScalarAsString(ref reader, out requested);
                        break;
                    case ContentHashProperty:
                        contentHashValid = TryReadScalarAsString(ref reader, out contentHash);
                        break;
                    case DependenciesProperty:
                        dependenciesValid = TryReadPackageDependencies(ref reader, out dependencies);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject
                || !typeValid
                || !resolvedValid
                || !requestedValid
                || !contentHashValid
                || !dependenciesValid)
            {
                return DependencyReadResult.Invalid;
            }

            var dependency = new LockFileDependency
            {
                Id = packageId,
                ContentHash = contentHash,
                Dependencies = dependencies
            };

            if (!string.IsNullOrEmpty(type)
                && Enum.TryParse(type, ignoreCase: true, out PackageDependencyType dependencyType))
            {
                dependency.Type = dependencyType;
            }

            if (!string.IsNullOrEmpty(resolved))
            {
                if (!NuGetVersion.TryParse(resolved, out NuGetVersion? resolvedVersion))
                {
                    return DependencyReadResult.Invalid;
                }

                dependency.ResolvedVersion = resolvedVersion;
            }

            if (requested is not null && requested.Length > 0)
            {
                if (!VersionRange.TryParse(requested, out VersionRange? requestedVersion))
                {
                    return DependencyReadResult.Invalid;
                }

                dependency.RequestedVersion = requestedVersion;
            }

            return new DependencyReadResult(dependency);
        }

        private static bool TryReadPackageDependencies(
            ref Utf8JsonStreamReader reader,
            out IList<PackageDependency> dependencies)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                dependencies = Array.Empty<PackageDependency>();
                return true;
            }

            var values = new List<KeyValuePair<string, string?>>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var validValues = new Dictionary<string, bool>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string packageId = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                bool isValid = TryReadScalarAsString(ref reader, out string? versionRange);
                SetValue(values, indexes, packageId, versionRange);
                validValues[packageId] = isValid;
            }

            var result = new List<PackageDependency>(values.Count);
            foreach (KeyValuePair<string, string?> value in values)
            {
                if (!validValues[value.Key])
                {
                    dependencies = Array.Empty<PackageDependency>();
                    return false;
                }

                VersionRange? versionRange = null;
                if (value.Value is not null
                    && !VersionRange.TryParse(value.Value, out versionRange))
                {
                    dependencies = Array.Empty<PackageDependency>();
                    return false;
                }

                result.Add(new PackageDependency(value.Key, versionRange));
            }

            dependencies = result;
            return reader.TokenType == JsonTokenType.EndObject;
        }

        private static int ReadVersion(ref Utf8JsonStreamReader reader)
        {
            return TryReadScalarAsString(ref reader, out string? version)
                && int.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    ? value
                    : int.MinValue;
        }

        private static bool TryReadScalarAsString(
            ref Utf8JsonStreamReader reader,
            out string? value)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    value = reader.ReadTokenAsString();
                    return true;
                default:
                    reader.Skip();
                    value = null;
                    return false;
            }
        }

        private static string ReadPropertyName(ref Utf8JsonStreamReader reader)
        {
            return reader.TokenType == JsonTokenType.PropertyName
                ? reader.GetString() ?? throw new JsonException()
                : throw new JsonException();
        }

        private static void ReadValue(ref Utf8JsonStreamReader reader)
        {
            if (!reader.Read())
            {
                throw new JsonException();
            }
        }

        private static IList<PackagesLockFileTarget> GetTargets(
            IEnumerable<KeyValuePair<string, TargetReadResult>> properties)
        {
            var targets = new List<PackagesLockFileTarget>();
            foreach (KeyValuePair<string, TargetReadResult> property in properties)
            {
                AddTarget(property.Value, targets);
            }

            return targets;
        }

        private static IList<PackagesLockFileTarget> GetTargets(
            IEnumerable<TargetReadResult> properties)
        {
            var targets = new List<PackagesLockFileTarget>();
            foreach (TargetReadResult property in properties)
            {
                AddTarget(property, targets);
            }

            return targets;
        }

        private static void AddTarget(
            TargetReadResult result,
            List<PackagesLockFileTarget> targets)
        {
            if (!result.IsValid)
            {
                throw new JsonException();
            }

            if (result.Target is not null)
            {
                targets.Add(result.Target);
            }
        }

        private static void SetValue<T>(
            List<KeyValuePair<string, T>> values,
            Dictionary<string, int> indexes,
            string name,
            T value)
        {
            var property = new KeyValuePair<string, T>(name, value);
            if (indexes.TryGetValue(name, out int index))
            {
                values[index] = property;
            }
            else
            {
                indexes.Add(name, values.Count);
                values.Add(property);
            }
        }

        private readonly struct TargetReadResult
        {
            internal static readonly TargetReadResult Ignored = new(isValid: true);
            internal static readonly TargetReadResult Invalid = new(isValid: false);

            internal TargetReadResult(PackagesLockFileTarget target)
            {
                IsValid = true;
                Target = target;
            }

            private TargetReadResult(bool isValid)
            {
                IsValid = isValid;
                Target = null;
            }

            internal bool IsValid { get; }

            internal PackagesLockFileTarget? Target { get; }
        }

        private readonly struct DependencyReadResult
        {
            internal static readonly DependencyReadResult Invalid = new();

            internal DependencyReadResult(LockFileDependency dependency)
            {
                IsValid = true;
                Dependency = dependency;
            }

            internal bool IsValid { get; }

            internal LockFileDependency? Dependency { get; }
        }
    }
}

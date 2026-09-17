// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using NuGet.Frameworks;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.RuntimeModel
{
    public static partial class JsonRuntimeFormat
    {
        /// <summary>
        /// Reads a runtime graph from a JSON stream in a single pass.
        /// </summary>
        private static RuntimeGraph ReadRuntimeGraph(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType == JsonTokenType.None)
            {
                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            List<RuntimeDescription>? runtimes = null;
            List<CompatibilityProfile>? supports = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = ReadPropertyName(ref reader);
                ReadValue(ref reader);

                if (propertyName == "runtimes")
                {
                    runtimes = ReadRuntimeDescriptions(ref reader);
                }
                else if (propertyName == "supports")
                {
                    supports = ReadCompatibilityProfiles(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject
                || reader.Read())
            {
                throw new JsonException();
            }

            return new RuntimeGraph(
                runtimes is null ? Array.Empty<RuntimeDescription>() : runtimes,
                supports is null ? Array.Empty<CompatibilityProfile>() : supports);
        }

        private static List<RuntimeDescription> ReadRuntimeDescriptions(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return [];
            }

            var values = new OrderedPropertyValues<RuntimeDescription>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string runtimeIdentifier = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                values.Set(runtimeIdentifier, ReadRuntimeDescription(ref reader, runtimeIdentifier));
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return values.GetValues() ?? throw new JsonException();
        }

        private static RuntimeDescription? ReadRuntimeDescription(
            ref Utf8JsonStreamReader reader,
            string runtimeIdentifier)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return new RuntimeDescription(runtimeIdentifier);
            }

            List<string>? inheritedRuntimes = null;
            bool inheritedRuntimesValid = true;
            var dependencySets = new OrderedPropertyValues<RuntimeDependencySet>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = ReadPropertyName(ref reader);
                ReadValue(ref reader);

                if (propertyName == "#import")
                {
                    inheritedRuntimes = ReadStringArray(ref reader);
                    inheritedRuntimesValid = inheritedRuntimes is not null;
                }
                else
                {
                    dependencySets.Set(propertyName, ReadRuntimeDependencySet(ref reader, propertyName));
                }
            }

            List<RuntimeDependencySet>? sets = dependencySets.GetValues();
            if (reader.TokenType != JsonTokenType.EndObject || !inheritedRuntimesValid || sets is null)
            {
                return null;
            }

            return new RuntimeDescription(runtimeIdentifier, inheritedRuntimes, sets);
        }

        private static RuntimeDependencySet? ReadRuntimeDependencySet(
            ref Utf8JsonStreamReader reader,
            string dependencySetId)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return new RuntimeDependencySet(dependencySetId);
            }

            var values = new OrderedPropertyValues<string>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string packageId = ReadPropertyName(ref reader);
                string? versionRange = ReadNextScalarAsString(ref reader);
                reader.Skip();
                values.Set(packageId, versionRange);
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                return null;
            }

            var dependencies = new List<RuntimePackageDependency>(values.Count);
            foreach (KeyValuePair<string, string?> value in values.Properties)
            {
                dependencies.Add(new RuntimePackageDependency(value.Key, VersionRange.Parse(value.Value!)));
            }

            return new RuntimeDependencySet(dependencySetId, dependencies);
        }

        private static List<CompatibilityProfile> ReadCompatibilityProfiles(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return [];
            }

            var values = new OrderedPropertyValues<CompatibilityProfile>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string profileName = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                values.Set(profileName, ReadCompatibilityProfile(ref reader, profileName));
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return values.GetValues() ?? throw new JsonException();
        }

        private static CompatibilityProfile? ReadCompatibilityProfile(
            ref Utf8JsonStreamReader reader,
            string profileName)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return new CompatibilityProfile(profileName);
            }

            var values = new OrderedPropertyValues<List<FrameworkRuntimePair>>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string frameworkName = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                values.Set(
                    frameworkName,
                    ReadFrameworkRuntimePairs(ref reader, NuGetFramework.Parse(frameworkName)));
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                return null;
            }

            var restoreContexts = new List<FrameworkRuntimePair>();
            foreach (KeyValuePair<string, List<FrameworkRuntimePair>?> value in values.Properties)
            {
                if (value.Value is null)
                {
                    return null;
                }

                restoreContexts.AddRange(value.Value);
            }

            return new CompatibilityProfile(profileName, restoreContexts);
        }

        private static List<FrameworkRuntimePair>? ReadFrameworkRuntimePairs(
            ref Utf8JsonStreamReader reader,
            NuGetFramework framework)
        {
            var values = new List<FrameworkRuntimePair>();
            if (reader.TokenType == JsonTokenType.String)
            {
                values.Add(new FrameworkRuntimePair(framework, reader.GetString()));
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                List<string?>? runtimeIdentifiers = ReadScalarArrayAsStrings(ref reader);
                if (runtimeIdentifiers is not null)
                {
                    foreach (string? runtimeIdentifier in runtimeIdentifiers)
                    {
                        values.Add(new FrameworkRuntimePair(framework, runtimeIdentifier));
                    }
                }
            }
            else
            {
                reader.Skip();
            }

            return values;
        }

        private static List<string>? ReadStringArray(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                return null;
            }

            List<string?>? values = ReadScalarArrayAsStrings(ref reader);
            if (values is null)
            {
                return [];
            }

            // RuntimeDescription historically permits null entries despite its non-nullable public contract.
            return values.ConvertAll(value => value!);
        }

        private static string? ReadNextScalarAsString(ref Utf8JsonStreamReader reader)
        {
            if (!reader.Read())
            {
                throw new JsonException();
            }

            return reader.ReadScalarAsInvariantString();
        }

        private static List<string?>? ReadScalarArrayAsStrings(ref Utf8JsonStreamReader reader)
        {
            List<string?>? values = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                values ??= [];
                values.Add(reader.ReadScalarAsInvariantString());
            }

            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException();
            }

            return values;
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

        /// <summary>
        /// Collects JSON object properties in source order while applying last-value-wins semantics
        /// to duplicate property names.
        /// </summary>
        /// <remarks>
        /// Newtonsoft.Json preserves the first occurrence's position when a later property with the
        /// same ordinal name replaces its value. The list preserves that order, and the index map
        /// avoids a linear search for each duplicate. Null values are retained until
        /// <see cref="GetValues"/> so callers can reject an invalid final value after all duplicates
        /// have been read.
        /// </remarks>
        private sealed class OrderedPropertyValues<T>
            where T : class
        {
            private readonly List<KeyValuePair<string, T?>> _properties = [];
            private readonly Dictionary<string, int> _indexes = new(StringComparer.Ordinal);

            internal int Count => _properties.Count;

            internal IEnumerable<KeyValuePair<string, T?>> Properties => _properties;

            internal void Set(string name, T? value)
            {
                var property = new KeyValuePair<string, T?>(name, value);
                if (_indexes.TryGetValue(name, out int index))
                {
                    _properties[index] = property;
                }
                else
                {
                    _indexes.Add(name, _properties.Count);
                    _properties.Add(property);
                }
            }

            internal List<T>? GetValues()
            {
                var values = new List<T>(_properties.Count);
                foreach (KeyValuePair<string, T?> property in _properties)
                {
                    if (property.Value is null)
                    {
                        return null;
                    }

                    values.Add(property.Value);
                }

                return values;
            }
        }
    }
}

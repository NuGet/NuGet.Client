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
            bool runtimesSeen = false;
            bool supportsSeen = false;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = ReadPropertyName(ref reader);
                ReadValue(ref reader);

                if (propertyName == "runtimes")
                {
                    runtimesSeen = true;
                    runtimes = ReadRuntimeDescriptions(ref reader);
                }
                else if (propertyName == "supports")
                {
                    supportsSeen = true;
                    supports = ReadCompatibilityProfiles(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject
                || reader.Read()
                || (runtimesSeen && runtimes is null)
                || (supportsSeen && supports is null))
            {
                throw new JsonException();
            }

            return new RuntimeGraph(
                runtimes is null ? Array.Empty<RuntimeDescription>() : runtimes,
                supports is null ? Array.Empty<CompatibilityProfile>() : supports);
        }

        private static List<RuntimeDescription>? ReadRuntimeDescriptions(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new List<RuntimeDescription>();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return null;
            }

            var values = new List<KeyValuePair<string, RuntimeDescription?>>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string runtimeIdentifier = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                SetValue(values, indexes, runtimeIdentifier, ReadRuntimeDescription(ref reader, runtimeIdentifier));
            }

            return reader.TokenType == JsonTokenType.EndObject ? GetValues(values) : null;
        }

        private static RuntimeDescription? ReadRuntimeDescription(
            ref Utf8JsonStreamReader reader,
            string runtimeIdentifier)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return null;
            }

            List<string>? inheritedRuntimes = null;
            bool inheritedRuntimesValid = true;
            var dependencySets = new List<KeyValuePair<string, RuntimeDependencySet?>>();
            var dependencySetIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

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
                    SetValue(
                        dependencySets,
                        dependencySetIndexes,
                        propertyName,
                        ReadRuntimeDependencySet(ref reader, propertyName));
                }
            }

            List<RuntimeDependencySet>? sets = GetValues(dependencySets);
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
                return null;
            }

            var values = new List<KeyValuePair<string, string?>>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string packageId = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                string? versionRange = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                reader.Skip();
                SetValue(values, indexes, packageId, versionRange);
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                return null;
            }

            var dependencies = new List<RuntimePackageDependency>(values.Count);
            foreach (KeyValuePair<string, string?> value in values)
            {
                if (value.Value is null)
                {
                    return null;
                }

                if (!VersionRange.TryParse(value.Value, out VersionRange? versionRange))
                {
                    return null;
                }

                dependencies.Add(new RuntimePackageDependency(value.Key, versionRange));
            }

            return new RuntimeDependencySet(dependencySetId, dependencies);
        }

        private static List<CompatibilityProfile>? ReadCompatibilityProfiles(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new List<CompatibilityProfile>();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return null;
            }

            var values = new List<KeyValuePair<string, CompatibilityProfile?>>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string profileName = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                SetValue(values, indexes, profileName, ReadCompatibilityProfile(ref reader, profileName));
            }

            return reader.TokenType == JsonTokenType.EndObject ? GetValues(values) : null;
        }

        private static CompatibilityProfile? ReadCompatibilityProfile(
            ref Utf8JsonStreamReader reader,
            string profileName)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new CompatibilityProfile(profileName);
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return null;
            }

            var values = new List<KeyValuePair<string, List<FrameworkRuntimePair>?>>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string frameworkName = ReadPropertyName(ref reader);
                ReadValue(ref reader);
                SetValue(
                    values,
                    indexes,
                    frameworkName,
                    ReadFrameworkRuntimePairs(ref reader, NuGetFramework.Parse(frameworkName)));
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                return null;
            }

            var restoreContexts = new List<FrameworkRuntimePair>();
            foreach (KeyValuePair<string, List<FrameworkRuntimePair>?> value in values)
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
                bool isValid = true;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        reader.Skip();
                        isValid = false;
                    }
                    else
                    {
                        values.Add(new FrameworkRuntimePair(framework, reader.GetString()));
                    }
                }

                if (reader.TokenType != JsonTokenType.EndArray || !isValid)
                {
                    return null;
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

            var values = new List<string>();
            bool isValid = true;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    reader.Skip();
                    isValid = false;
                }
                else
                {
                    values.Add(reader.GetString() ?? throw new JsonException());
                }
            }

            return reader.TokenType == JsonTokenType.EndArray && isValid ? values : null;
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

        private static void SetValue<T>(
            List<KeyValuePair<string, T?>> values,
            Dictionary<string, int> indexes,
            string name,
            T? value)
            where T : class
        {
            var property = new KeyValuePair<string, T?>(name, value);
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

        private static List<T>? GetValues<T>(List<KeyValuePair<string, T?>> properties)
            where T : class
        {
            var values = new List<T>(properties.Count);
            foreach (KeyValuePair<string, T?> property in properties)
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

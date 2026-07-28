// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGet.RuntimeModel
{
    internal sealed class RuntimeDescriptionCollectionJsonConverter : JsonConverter<List<RuntimeDescription>>
    {
        public override List<RuntimeDescription> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var runtimes = new List<RuntimeDescription>();
            if (reader.TokenType == JsonTokenType.Null)
            {
                return runtimes;
            }

            EnsureToken(reader.TokenType, JsonTokenType.StartObject);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                EnsureToken(reader.TokenType, JsonTokenType.PropertyName);
                string runtimeIdentifier = reader.GetString()!;
                ReadNext(ref reader);
                runtimes.Add(ReadRuntimeDescription(ref reader, runtimeIdentifier));
            }

            return runtimes;
        }

        public override void Write(
            Utf8JsonWriter writer,
            List<RuntimeDescription> value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        private static RuntimeDescription ReadRuntimeDescription(
            ref Utf8JsonReader reader,
            string runtimeIdentifier)
        {
            EnsureToken(reader.TokenType, JsonTokenType.StartObject);
            List<string>? inheritedRuntimes = null;
            List<RuntimeDependencySet>? dependencySets = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                EnsureToken(reader.TokenType, JsonTokenType.PropertyName);
                string propertyName = reader.GetString()!;
                ReadNext(ref reader);

                if (propertyName == "#import")
                {
                    inheritedRuntimes = ReadInheritedRuntimes(ref reader);
                }
                else
                {
                    dependencySets ??= new List<RuntimeDependencySet>();
                    dependencySets.Add(ReadRuntimeDependencySet(ref reader, propertyName));
                }
            }

            return new RuntimeDescription(runtimeIdentifier, inheritedRuntimes, dependencySets);
        }

        private static List<string> ReadInheritedRuntimes(ref Utf8JsonReader reader)
        {
            EnsureToken(reader.TokenType, JsonTokenType.StartArray);
            var inheritedRuntimes = new List<string>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                EnsureToken(reader.TokenType, JsonTokenType.String);
                inheritedRuntimes.Add(reader.GetString()!);
            }

            return inheritedRuntimes;
        }

        private static RuntimeDependencySet ReadRuntimeDependencySet(
            ref Utf8JsonReader reader,
            string dependencySetId)
        {
            EnsureToken(reader.TokenType, JsonTokenType.StartObject);
            var dependencies = new List<RuntimePackageDependency>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                EnsureToken(reader.TokenType, JsonTokenType.PropertyName);
                string dependencyId = reader.GetString()!;
                ReadNext(ref reader);
                EnsureToken(reader.TokenType, JsonTokenType.String);
                dependencies.Add(new RuntimePackageDependency(
                    dependencyId,
                    VersionRange.Parse(reader.GetString()!)));
            }

            return new RuntimeDependencySet(dependencySetId, dependencies);
        }

        private static void ReadNext(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                throw new JsonException();
            }
        }

        private static void EnsureToken(JsonTokenType actual, JsonTokenType expected)
        {
            if (actual != expected)
            {
                throw new JsonException();
            }
        }
    }

    internal sealed class CompatibilityProfileCollectionJsonConverter : JsonConverter<List<CompatibilityProfile>>
    {
        public override List<CompatibilityProfile> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var profiles = new List<CompatibilityProfile>();
            if (reader.TokenType == JsonTokenType.Null)
            {
                return profiles;
            }

            EnsureToken(reader.TokenType, JsonTokenType.StartObject);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                EnsureToken(reader.TokenType, JsonTokenType.PropertyName);
                string profileName = reader.GetString()!;
                ReadNext(ref reader);
                profiles.Add(ReadCompatibilityProfile(ref reader, profileName));
            }

            return profiles;
        }

        public override void Write(
            Utf8JsonWriter writer,
            List<CompatibilityProfile> value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        private static CompatibilityProfile ReadCompatibilityProfile(
            ref Utf8JsonReader reader,
            string profileName)
        {
            var restoreContexts = new List<FrameworkRuntimePair>();
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new CompatibilityProfile(profileName, restoreContexts);
            }

            EnsureToken(reader.TokenType, JsonTokenType.StartObject);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                EnsureToken(reader.TokenType, JsonTokenType.PropertyName);
                NuGetFramework framework = NuGetFramework.Parse(reader.GetString()!);
                ReadNext(ref reader);
                ReadRestoreContexts(ref reader, framework, restoreContexts);
            }

            return new CompatibilityProfile(profileName, restoreContexts);
        }

        private static void ReadRestoreContexts(
            ref Utf8JsonReader reader,
            NuGetFramework framework,
            List<FrameworkRuntimePair> restoreContexts)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                restoreContexts.Add(new FrameworkRuntimePair(framework, reader.GetString()));
                return;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    EnsureToken(reader.TokenType, JsonTokenType.String);
                    restoreContexts.Add(new FrameworkRuntimePair(framework, reader.GetString()));
                }

                return;
            }

            reader.Skip();
        }

        private static void ReadNext(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                throw new JsonException();
            }
        }

        private static void EnsureToken(JsonTokenType actual, JsonTokenType expected)
        {
            if (actual != expected)
            {
                throw new JsonException();
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Frameworks;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.RuntimeModel
{
    internal sealed class RuntimeGraphJsonModelConverter : JsonConverter<RuntimeGraphJsonModel>
    {
        public override RuntimeGraphJsonModel Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // Parse the complete object first so duplicate properties use the final value, as Newtonsoft.Json does.
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            var model = new RuntimeGraphJsonModel();

            foreach (JsonProperty property in document.RootElement.GetUniqueProperties())
            {
                if (property.NameEquals("runtimes"))
                {
                    model.Runtimes = RuntimeDescriptionCollectionJsonConverter.Read(property.Value);
                }
                else if (property.NameEquals("supports"))
                {
                    model.Supports = CompatibilityProfileCollectionJsonConverter.Read(property.Value);
                }
            }

            return model;
        }

        public override void Write(
            Utf8JsonWriter writer,
            RuntimeGraphJsonModel value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }

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

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return Read(document.RootElement);
        }

        internal static List<RuntimeDescription> Read(JsonElement json)
        {
            var runtimes = new List<RuntimeDescription>();

            if (json.ValueKind == JsonValueKind.Null)
            {
                return runtimes;
            }

            foreach (JsonProperty runtime in json.GetUniqueProperties())
            {
                runtimes.Add(ReadRuntimeDescription(runtime.Name, runtime.Value));
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

        private static RuntimeDescription ReadRuntimeDescription(string runtimeIdentifier, JsonElement json)
        {
            List<string>? inheritedRuntimes = null;
            List<RuntimeDependencySet>? dependencySets = null;

            foreach (JsonProperty property in json.GetUniqueProperties())
            {
                if (property.Name == "#import")
                {
                    inheritedRuntimes = ReadInheritedRuntimes(property.Value);
                }
                else
                {
                    dependencySets ??= new List<RuntimeDependencySet>();
                    dependencySets.Add(ReadRuntimeDependencySet(property.Name, property.Value));
                }
            }

            return new RuntimeDescription(runtimeIdentifier, inheritedRuntimes, dependencySets);
        }

        private static List<string> ReadInheritedRuntimes(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException();
            }

            var inheritedRuntimes = new List<string>();

            foreach (JsonElement runtime in json.EnumerateArray())
            {
                inheritedRuntimes.Add(runtime.GetRequiredString());
            }

            return inheritedRuntimes;
        }

        private static RuntimeDependencySet ReadRuntimeDependencySet(string dependencySetId, JsonElement json)
        {
            var dependencies = new List<RuntimePackageDependency>();

            foreach (JsonProperty dependency in json.GetUniqueProperties())
            {
                dependencies.Add(
                    new RuntimePackageDependency(
                        dependency.Name,
                        VersionRange.Parse(dependency.Value.GetRequiredString())));
            }

            return new RuntimeDependencySet(dependencySetId, dependencies);
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

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return Read(document.RootElement);
        }

        internal static List<CompatibilityProfile> Read(JsonElement json)
        {
            var profiles = new List<CompatibilityProfile>();

            if (json.ValueKind == JsonValueKind.Null)
            {
                return profiles;
            }

            foreach (JsonProperty profile in json.GetUniqueProperties())
            {
                profiles.Add(ReadCompatibilityProfile(profile.Name, profile.Value));
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

        private static CompatibilityProfile ReadCompatibilityProfile(string profileName, JsonElement json)
        {
            var restoreContexts = new List<FrameworkRuntimePair>();
            if (json.ValueKind == JsonValueKind.Null)
            {
                return new CompatibilityProfile(profileName, restoreContexts);
            }

            foreach (JsonProperty frameworkProperty in json.GetUniqueProperties())
            {
                NuGetFramework framework = NuGetFramework.Parse(frameworkProperty.Name);
                ReadRestoreContexts(frameworkProperty.Value, framework, restoreContexts);
            }

            return new CompatibilityProfile(profileName, restoreContexts);
        }

        private static void ReadRestoreContexts(
            JsonElement json,
            NuGetFramework framework,
            List<FrameworkRuntimePair> restoreContexts)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                restoreContexts.Add(new FrameworkRuntimePair(framework, json.GetString()));
                return;
            }

            if (json.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement runtime in json.EnumerateArray())
                {
                    restoreContexts.Add(
                        new FrameworkRuntimePair(
                            framework,
                            runtime.GetRequiredString()));
                }

                return;
            }

        }
    }
}

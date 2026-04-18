// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Converters
{
    /// <remarks>No NSJ equivalent.</remarks>
    internal sealed class PackageDependencyGroupStjConverter : JsonConverter<PackageDependencyGroup>
    {
        private static readonly PackageDependencyStjConverter _dependencyConverter = new();

        public override PackageDependencyGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            NuGetFramework? targetFramework = null;
            var packages = new List<PackageDependency>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propName = reader.GetString();
                reader.Read();

                if (string.Equals(propName, JsonProperties.TargetFramework, StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType != JsonTokenType.Null)
                    {
                        var fw = reader.GetString();
                        targetFramework = string.IsNullOrEmpty(fw) ? null : NuGetFramework.Parse(fw!);
                    }
                }
                else if (string.Equals(propName, JsonProperties.Dependencies, StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            packages.Add(_dependencyConverter.Read(ref reader, typeof(PackageDependency), options));
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                else
                {
                    reader.Skip();
                }
            }

            return new PackageDependencyGroup(targetFramework ?? NuGetFramework.AnyFramework, packages);
        }

        public override void Write(Utf8JsonWriter writer, PackageDependencyGroup value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(JsonProperties.TargetFramework, value.TargetFramework.GetShortFolderName());
            writer.WriteStartArray(JsonProperties.Dependencies);
            foreach (var pkg in value.Packages)
            {
                _dependencyConverter.Write(writer, pkg, options);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}

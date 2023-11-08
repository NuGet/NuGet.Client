// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="LockFileTargetLibrary"/>
    /// </summary>
    internal class LockFileTargetLibraryConverter : JsonConverter<LockFileTargetLibrary>
    {
        private static readonly byte[] Utf8Type = Encoding.UTF8.GetBytes("type");
        private static readonly byte[] Utf8Framework = Encoding.UTF8.GetBytes("framework");
        private static readonly byte[] Utf8Dependencies = Encoding.UTF8.GetBytes("dependencies");
        private static readonly byte[] Utf8FrameworkAssemblies = Encoding.UTF8.GetBytes("frameworkAssemblies");
        private static readonly byte[] Utf8Runtime = Encoding.UTF8.GetBytes("runtime");
        private static readonly byte[] Utf8Compile = Encoding.UTF8.GetBytes("compile");
        private static readonly byte[] Utf8Resource = Encoding.UTF8.GetBytes("resource");
        private static readonly byte[] Utf8Native = Encoding.UTF8.GetBytes("native");
        private static readonly byte[] Utf8Build = Encoding.UTF8.GetBytes("build");
        private static readonly byte[] Utf8BuildMultiTargeting = Encoding.UTF8.GetBytes("buildMultiTargeting");
        private static readonly byte[] Utf8ContentFiles = Encoding.UTF8.GetBytes("contentFiles");
        private static readonly byte[] Utf8RuntimeTargets = Encoding.UTF8.GetBytes("runtimeTargets");
        private static readonly byte[] Utf8Tools = Encoding.UTF8.GetBytes("tools");
        private static readonly byte[] Utf8Embed = Encoding.UTF8.GetBytes("embed");
        private static readonly byte[] Utf8FrameworkReferences = Encoding.UTF8.GetBytes("frameworkReferences");

        public override LockFileTargetLibrary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(LockFileTargetLibrary))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var lockFileTargetLibrary = new LockFileTargetLibrary();

            //We want to read the property name right away
            var propertyName = reader.GetString();
#pragma warning disable CA1307 // Specify StringComparison
            int slashIndex = propertyName.IndexOf('/');
#pragma warning restore CA1307 // Specify StringComparison
            if (slashIndex == -1)
            {
                lockFileTargetLibrary.Name = propertyName;
            }
            else
            {
                lockFileTargetLibrary.Name = propertyName.Substring(0, slashIndex);
                lockFileTargetLibrary.Version = NuGetVersion.Parse(propertyName.Substring(slashIndex + 1));
            }

            var stringListDefaultConverter = (JsonConverter<IList<string>>)options.GetConverter(typeof(IList<string>));
            var lockFileItemListConverter = (JsonConverter<IList<LockFileItem>>)options.GetConverter(typeof(IList<LockFileItem>));
            var lockFileItemContentFileListConverter = (JsonConverter<IList<LockFileContentFile>>)options.GetConverter(typeof(IList<LockFileContentFile>));
            var lockFileRuntimeTargetConverter = (JsonConverter<IList<LockFileRuntimeTarget>>)options.GetConverter(typeof(IList<LockFileRuntimeTarget>));

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        if (reader.ValueTextEquals(Utf8Type))
                        {
                            reader.Read();
                            lockFileTargetLibrary.Type = reader.GetString();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Framework))
                        {
                            reader.Read();
                            lockFileTargetLibrary.Framework = reader.GetString();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Dependencies))
                        {
                            reader.Read();
                            if (ReadPackageDependencyList(ref reader) is { Count: not 0 } packageDependencies)
                            {
                                lockFileTargetLibrary.Dependencies = packageDependencies;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8FrameworkAssemblies))
                        {
                            reader.Read();
                            lockFileTargetLibrary.FrameworkAssemblies = stringListDefaultConverter.Read(ref reader, typeof(IList<string>), options);
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Runtime))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } runtimeAssemblies)
                            {
                                lockFileTargetLibrary.RuntimeAssemblies = runtimeAssemblies;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Compile))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } compileTimeAssemblies)
                            {
                                lockFileTargetLibrary.CompileTimeAssemblies = compileTimeAssemblies;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Resource))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } resourceAssemblies)
                            {
                                lockFileTargetLibrary.ResourceAssemblies = resourceAssemblies;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Native))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } nativeLibraries)
                            {
                                lockFileTargetLibrary.NativeLibraries = nativeLibraries;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Build))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } build)
                            {
                                lockFileTargetLibrary.Build = build;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8BuildMultiTargeting))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } buildMultiTargeting)
                            {
                                lockFileTargetLibrary.BuildMultiTargeting = buildMultiTargeting;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8ContentFiles))
                        {
                            reader.Read();
                            if (lockFileItemContentFileListConverter.Read(ref reader, typeof(IList<LockFileContentFile>), options) is { Count: not 0 } contentFiles)
                            {
                                lockFileTargetLibrary.ContentFiles = contentFiles;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8RuntimeTargets))
                        {
                            reader.Read();
                            if (lockFileRuntimeTargetConverter.Read(ref reader, typeof(IList<LockFileRuntimeTarget>), options) is { Count: not 0 } runtimeTargets)
                            {
                                lockFileTargetLibrary.RuntimeTargets = runtimeTargets;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Tools))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } toolsAssemblies)
                            {
                                lockFileTargetLibrary.ToolsAssemblies = toolsAssemblies;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Embed))
                        {
                            reader.Read();
                            if (lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options) is { Count: not 0 } embedAssemblies)
                            {
                                lockFileTargetLibrary.EmbedAssemblies = embedAssemblies;
                            }
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8FrameworkReferences))
                        {
                            reader.Read();
                            if (stringListDefaultConverter.Read(ref reader, typeof(IList<string>), options) is { Count: not 0 } frameworkReferences)
                            {
                                lockFileTargetLibrary.FrameworkReferences = frameworkReferences;
                            }
                            break;
                        }
                        break;
                    case JsonTokenType.EndObject:
                        lockFileTargetLibrary.Freeze();
                        return lockFileTargetLibrary;
                    default:
                        throw new JsonException("Unexpected token " + reader.TokenType);
                }
            }
            lockFileTargetLibrary.Freeze();
            return lockFileTargetLibrary;
        }

        private IList<PackageDependency> ReadPackageDependencyList(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return new List<PackageDependency>(0);
            }

            reader.Read();
            var packageDependencies = new List<PackageDependency>();
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString();
                    reader.Read();
                    string versionString = reader.GetString();

                    packageDependencies.Add(new PackageDependency(
                        propertyName,
                        versionString == null ? null : VersionRange.Parse(versionString)));
                }
                reader.Read();
            }
            return packageDependencies;
        }

        public override void Write(Utf8JsonWriter writer, LockFileTargetLibrary value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue("hi");
            }
        }
    }
}

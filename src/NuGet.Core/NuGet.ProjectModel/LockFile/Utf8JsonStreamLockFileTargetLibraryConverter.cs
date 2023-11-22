// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
    internal class Utf8JsonStreamLockFileTargetLibraryConverter : Utf8JsonStreamReaderConverter<LockFileTargetLibrary>
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

        private IList<PackageDependency> ReadPackageDependencyList(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return new List<PackageDependency>(0);
            }

            var packageDependencies = new List<PackageDependency>();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString();
                string versionString = reader.ReadNextTokenAsString();

                packageDependencies.Add(new PackageDependency(
                    propertyName,
                    versionString == null ? null : VersionRange.Parse(versionString)));
            }
            return packageDependencies;
        }

        public override LockFileTargetLibrary Read(ref Utf8JsonStreamReader reader)
        {
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

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals(Utf8Type))
                {
                    lockFileTargetLibrary.Type = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8Framework))
                {
                    lockFileTargetLibrary.Framework = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8Dependencies))
                {
                    reader.Read();
                    if (ReadPackageDependencyList(ref reader) is { Count: not 0 } packageDependencies)
                    {
                        lockFileTargetLibrary.Dependencies = packageDependencies;
                    }
                }
                else if (reader.ValueTextEquals(Utf8FrameworkAssemblies))
                {
                    reader.Read();
                    lockFileTargetLibrary.FrameworkAssemblies = reader.ReadStringArrayAsIList(new List<string>());
                }
                else if (reader.ValueTextEquals(Utf8Runtime))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } runtimeAssemblies)
                    {
                        lockFileTargetLibrary.RuntimeAssemblies = runtimeAssemblies;
                    }
                }
                else if (reader.ValueTextEquals(Utf8Compile))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } compileTimeAssemblies)
                    {
                        lockFileTargetLibrary.CompileTimeAssemblies = compileTimeAssemblies;
                    }
                }
                else if (reader.ValueTextEquals(Utf8Resource))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } resourceAssemblies)
                    {
                        lockFileTargetLibrary.ResourceAssemblies = resourceAssemblies;
                    }
                }
                else if (reader.ValueTextEquals(Utf8Native))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } nativeLibraries)
                    {
                        lockFileTargetLibrary.NativeLibraries = nativeLibraries;
                    }
                }
                else if (reader.ValueTextEquals(Utf8Build))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } build)
                    {
                        lockFileTargetLibrary.Build = build;
                    }
                }
                else if (reader.ValueTextEquals(Utf8BuildMultiTargeting))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } buildMultiTargeting)
                    {
                        lockFileTargetLibrary.BuildMultiTargeting = buildMultiTargeting;
                    }
                }
                else if (reader.ValueTextEquals(Utf8ContentFiles))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileContentFile>(Utf8JsonReaderExtensions.LockFileContentFileConverter) is { Count: not 0 } contentFiles)
                    {
                        lockFileTargetLibrary.ContentFiles = contentFiles;
                    }
                }
                else if (reader.ValueTextEquals(Utf8RuntimeTargets))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileRuntimeTarget>(Utf8JsonReaderExtensions.LockFileRuntimeTargetConverter) is { Count: not 0 } runtimeTargets)
                    {
                        lockFileTargetLibrary.RuntimeTargets = runtimeTargets;
                    }
                }
                else if (reader.ValueTextEquals(Utf8Tools))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } toolsAssemblies)
                    {
                        lockFileTargetLibrary.ToolsAssemblies = toolsAssemblies;
                    }
                }
                else if (reader.ValueTextEquals(Utf8Embed))
                {
                    reader.Read();
                    if (reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter) is { Count: not 0 } embedAssemblies)
                    {
                        lockFileTargetLibrary.EmbedAssemblies = embedAssemblies;
                    }
                }
                else if (reader.ValueTextEquals(Utf8FrameworkReferences))
                {
                    reader.Read();
                    if (reader.ReadStringArrayAsIList(new List<string>()) is { Count: not 0 } frameworkReferences)
                    {
                        lockFileTargetLibrary.FrameworkReferences = frameworkReferences;
                    }
                }
                else
                {
                    reader.TrySkip();
                }
            }
            lockFileTargetLibrary.Freeze();
            return lockFileTargetLibrary;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="LockFile"/>
    /// </summary>
    internal class LockFileConverter : JsonConverter<LockFile>
    {
        private static readonly byte[] Utf8Version = Encoding.UTF8.GetBytes("version");
        private static readonly byte[] Utf8Libraries = Encoding.UTF8.GetBytes("libraries");
        private static readonly byte[] Utf8Targets = Encoding.UTF8.GetBytes("targets");
        private static readonly byte[] Utf8ProjectFileDependencyGroups = Encoding.UTF8.GetBytes("projectFileDependencyGroups");
        private static readonly byte[] Utf8PackageFolders = Encoding.UTF8.GetBytes("packageFolders");
        private static readonly byte[] Utf8Project = Encoding.UTF8.GetBytes("project");
        private static readonly byte[] Utf8CentralTransitiveDependencyGroups = Encoding.UTF8.GetBytes("centralTransitiveDependencyGroups");
        private static readonly byte[] Utf8Logs = Encoding.UTF8.GetBytes("logs");

        public override LockFile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(LockFile))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            var lockFile = new LockFile();
            var assetLogMessageConverter = (JsonConverter<AssetsLogMessage>)options.GetConverter(typeof(AssetsLogMessage));


            while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals(Utf8Version))
                {
                    reader.ReadNextToken();
                    if (reader.TryGetInt32(out int version))
                    {
                        lockFile.Version = version;
                    }
                    else
                    {
                        lockFile.Version = int.MinValue;
                    }
                }
                else if (reader.ValueTextEquals(Utf8Libraries))
                {
                    reader.ReadNextToken();
                    lockFile.Libraries = reader.ReadObjectAsList<LockFileLibrary>(options);
                }
                else if (reader.ValueTextEquals(Utf8Targets))
                {
                    reader.ReadNextToken();
                    lockFile.Targets = reader.ReadObjectAsList<LockFileTarget>(options);
                }
                else if (reader.ValueTextEquals(Utf8ProjectFileDependencyGroups))
                {
                    reader.ReadNextToken();
                    lockFile.ProjectFileDependencyGroups = reader.ReadObjectAsList<ProjectFileDependencyGroup>(options);
                }
                else if (reader.ValueTextEquals(Utf8PackageFolders))
                {
                    reader.ReadNextToken();
                    lockFile.PackageFolders = reader.ReadObjectAsList<LockFileItem>(options);
                }
                else if (reader.ValueTextEquals(Utf8Project))
                {
                    lockFile.PackageSpec = JsonPackageSpecReader.GetPackageSpec(
                        ref reader,
                        options,
                        name: null,
                        packageSpecPath: null,
                        snapshotValue: null);
                }
                else if (reader.ValueTextEquals(Utf8CentralTransitiveDependencyGroups))
                {
                    var results = new List<CentralTransitiveDependencyGroup>();
                    if (reader.ReadNextToken() && reader.TokenType == JsonTokenType.StartObject)
                    {
                        while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.PropertyName)
                        {
                            var frameworkPropertyName = reader.GetString();
                            NuGetFramework framework = NuGetFramework.Parse(frameworkPropertyName);
                            var dependencies = new List<LibraryDependency>();

                            JsonPackageSpecReader.ReadCentralTransitiveDependencyGroup(
                                jsonReader: ref reader,
                                results: dependencies,
                                packageSpecPath: string.Empty);
                            results.Add(new CentralTransitiveDependencyGroup(framework, dependencies));
                        }
                    }
                    lockFile.CentralTransitiveDependencyGroups = results;
                }
                else if (reader.ValueTextEquals(Utf8Logs))
                {
                    reader.ReadArrayOfObjects<AssetsLogMessage, IAssetsLogMessage>(options, lockFile.LogMessages);
                }
                else
                {
                    reader.Skip();
                }
            }

            if (!string.IsNullOrEmpty(lockFile.PackageSpec?.RestoreMetadata?.ProjectPath) && lockFile.LogMessages.Count > 0)
            {
                foreach (AssetsLogMessage message in lockFile.LogMessages.Where(x => string.IsNullOrEmpty(x.ProjectPath)))
                {
                    message.FilePath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;
                }
            }

            return lockFile;
        }

        public override void Write(Utf8JsonWriter writer, LockFile value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}

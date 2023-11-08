// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Common;
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
            var lockFileLibraryConverter = (JsonConverter<IList<LockFileLibrary>>)options.GetConverter(typeof(IList<LockFileLibrary>));
            var lockFileTargetConverter = (JsonConverter<IList<LockFileTarget>>)options.GetConverter(typeof(IList<LockFileTarget>));
            var projectFileDependencyGroupConverter = (JsonConverter<IList<ProjectFileDependencyGroup>>)options.GetConverter(typeof(IList<ProjectFileDependencyGroup>));
            var lockFileItemListConverter = (JsonConverter<IList<LockFileItem>>)options.GetConverter(typeof(IList<LockFileItem>));

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
                    lockFile.Libraries = lockFileLibraryConverter.Read(ref reader, typeof(IList<LockFileLibrary>), options);
                }
                else if (reader.ValueTextEquals(Utf8Targets))
                {
                    reader.ReadNextToken();
                    lockFile.Targets = lockFileTargetConverter.Read(ref reader, typeof(IList<LockFileTarget>), options);
                }
                else if (reader.ValueTextEquals(Utf8ProjectFileDependencyGroups))
                {
                    reader.ReadNextToken();
                    lockFile.ProjectFileDependencyGroups = projectFileDependencyGroupConverter.Read(ref reader, typeof(IList<ProjectFileDependencyGroup>), options);
                }
                else if (reader.ValueTextEquals(Utf8PackageFolders))
                {
                    reader.ReadNextToken();
                    lockFile.PackageFolders = lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options);
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
                    if (reader.ReadNextToken() && reader.TokenType == JsonTokenType.StartArray)
                    {
                        while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.StartObject)
                        {
                            var isValid = true;
                            LogLevel level = default;
                            NuGetLogCode code = default;
                            //matching default warning level when AssetLogMessage object is created
                            WarningLevel warningLevel = WarningLevel.Severe;
                            string message = default;
                            string filePath = default;
                            int startLineNumber = default;
                            int startColNumber = default;
                            int endLineNumber = default;
                            int endColNumber = default;
                            string libraryId = default;
                            IReadOnlyList<string> targetGraphs = null;

                            while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (!isValid)
                                {
                                    reader.Skip();
                                }
                                if (reader.ValueTextEquals(LogMessageProperties.LEVEL))
                                {
                                    var levelString = reader.ReadNextTokenAsString();
                                    isValid &= Enum.TryParse(levelString, out level);
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.CODE))
                                {
                                    var codeString = reader.ReadNextTokenAsString();
                                    isValid &= Enum.TryParse(codeString, out code);
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.WARNING_LEVEL))
                                {
                                    reader.ReadNextToken();
                                    warningLevel = (WarningLevel)Enum.ToObject(typeof(WarningLevel), reader.GetInt32());
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.FILE_PATH))
                                {
                                    filePath = reader.ReadNextTokenAsString();
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.START_LINE_NUMBER))
                                {
                                    reader.ReadNextToken();
                                    startLineNumber = reader.GetInt32();
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.START_COLUMN_NUMBER))
                                {
                                    reader.ReadNextToken();
                                    startColNumber = reader.GetInt32();
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.END_LINE_NUMBER))
                                {
                                    reader.ReadNextToken();
                                    endLineNumber = reader.GetInt32();
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.END_COLUMN_NUMBER))
                                {
                                    reader.ReadNextToken();
                                    endColNumber = reader.GetInt32();
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.MESSAGE))
                                {
                                    message = reader.ReadNextTokenAsString();
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.LIBRARY_ID))
                                {
                                    libraryId = reader.ReadNextTokenAsString();
                                }
                                else if (reader.ValueTextEquals(LogMessageProperties.TARGET_GRAPHS))
                                {
                                    targetGraphs = reader.ReadStringArrayAsList();
                                }
                                else
                                {
                                    reader.Skip();
                                }
                            }
                            if (isValid)
                            {
                                var assetLogMessage = new AssetsLogMessage(level, code, message)
                                {
                                    TargetGraphs = targetGraphs ?? new List<string>(0),
                                    FilePath = filePath,
                                    EndColumnNumber = endColNumber,
                                    EndLineNumber = endLineNumber,
                                    LibraryId = libraryId,
                                    StartColumnNumber = startColNumber,
                                    StartLineNumber = startLineNumber,
                                    WarningLevel = warningLevel
                                };
                                lockFile.LogMessages.Add(assetLogMessage);
                            }
                        }
                    }
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

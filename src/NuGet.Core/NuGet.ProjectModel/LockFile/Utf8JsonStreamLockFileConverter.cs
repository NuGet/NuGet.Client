// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
    internal class Utf8JsonStreamLockFileConverter : Utf8JsonStreamReaderConverter<LockFile>
    {
        private static readonly byte[] Utf8Version = Encoding.UTF8.GetBytes("version");
        private static readonly byte[] Utf8Libraries = Encoding.UTF8.GetBytes("libraries");
        private static readonly byte[] Utf8Targets = Encoding.UTF8.GetBytes("targets");
        private static readonly byte[] Utf8ProjectFileDependencyGroups = Encoding.UTF8.GetBytes("projectFileDependencyGroups");
        private static readonly byte[] Utf8PackageFolders = Encoding.UTF8.GetBytes("packageFolders");
        private static readonly byte[] Utf8Project = Encoding.UTF8.GetBytes("project");
        private static readonly byte[] Utf8CentralTransitiveDependencyGroups = Encoding.UTF8.GetBytes("centralTransitiveDependencyGroups");
        private static readonly byte[] Utf8Logs = Encoding.UTF8.GetBytes("logs");

        public override LockFile Read(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            var lockFile = new LockFile();

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals(Utf8Version))
                {
                    reader.Read();
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
                    reader.Read();
                    lockFile.Libraries = reader.ReadObjectAsList<LockFileLibrary>(Utf8JsonReaderExtensions.LockFileLibraryConverter);
                }
                else if (reader.ValueTextEquals(Utf8Targets))
                {
                    reader.Read();
                    lockFile.Targets = reader.ReadObjectAsList<LockFileTarget>(Utf8JsonReaderExtensions.LockFileTargetConverter);
                }
                else if (reader.ValueTextEquals(Utf8ProjectFileDependencyGroups))
                {
                    reader.Read();
                    lockFile.ProjectFileDependencyGroups = reader.ReadObjectAsList<ProjectFileDependencyGroup>(Utf8JsonReaderExtensions.ProjectFileDepencencyGroupConverter);
                }
                else if (reader.ValueTextEquals(Utf8PackageFolders))
                {
                    reader.Read();
                    lockFile.PackageFolders = reader.ReadObjectAsList<LockFileItem>(Utf8JsonReaderExtensions.LockFileItemConverter);
                }
                else if (reader.ValueTextEquals(Utf8Project))
                {
                    reader.Read();
                    lockFile.PackageSpec = Utf8JsonStreamPackageSpecReader.GetPackageSpec(
                        ref reader,
                        name: null,
                        packageSpecPath: null,
                        snapshotValue: null);
                }
                else if (reader.ValueTextEquals(Utf8CentralTransitiveDependencyGroups))
                {
                    var results = new List<CentralTransitiveDependencyGroup>();
                    if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                    {
                        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                        {
                            var frameworkPropertyName = reader.GetString();
                            NuGetFramework framework = NuGetFramework.Parse(frameworkPropertyName);
                            var dependencies = new List<LibraryDependency>();

                            Utf8JsonStreamPackageSpecReader.ReadCentralTransitiveDependencyGroup(
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
                    reader.ReadArrayOfObjects<AssetsLogMessage, IAssetsLogMessage>(lockFile.LogMessages, Utf8JsonReaderExtensions.AssetsLogMessageConverter);
                }
                else
                {
                    reader.TrySkip();
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
    }
}

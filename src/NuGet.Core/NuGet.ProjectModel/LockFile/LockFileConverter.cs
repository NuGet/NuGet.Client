// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="LockFile"/>
    /// </summary>
    internal class PackageSpecConverter : JsonConverter<LockFile>
    {
        private static readonly byte[] Utf8Version = Encoding.UTF8.GetBytes("version");
        private static readonly byte[] Utf8Libraries = Encoding.UTF8.GetBytes("libraries");
        private static readonly byte[] Utf8Targets = Encoding.UTF8.GetBytes("targets");
        private static readonly byte[] Utf8ProjectFileDependencyGroups = Encoding.UTF8.GetBytes("projectFileDependencyGroups");
        private static readonly byte[] Utf8PackageFolders = Encoding.UTF8.GetBytes("packageFolders");
        private static readonly byte[] Utf8Project = Encoding.UTF8.GetBytes("project");
        private static readonly byte[] Utf8CentralTransitiveDependencyGroups = Encoding.UTF8.GetBytes("centralTransitiveDependencyGroups");
        //private static readonly byte[] Utf8 = Encoding.UTF8.GetBytes("");

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

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
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
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Libraries))
                        {
                            reader.Read();
                            lockFile.Libraries = lockFileLibraryConverter.Read(ref reader, typeof(IList<LockFileLibrary>), options);
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Targets))
                        {
                            reader.Read();
                            lockFile.Targets = lockFileTargetConverter.Read(ref reader, typeof(IList<LockFileTarget>), options);
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8ProjectFileDependencyGroups))
                        {
                            reader.Read();
                            lockFile.ProjectFileDependencyGroups = projectFileDependencyGroupConverter.Read(ref reader, typeof(IList<ProjectFileDependencyGroup>), options);
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8PackageFolders))
                        {
                            reader.Read();
                            lockFile.PackageFolders = lockFileItemListConverter.Read(ref reader, typeof(IList<LockFileItem>), options);
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Project))
                        {
                            reader.Skip();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8CentralTransitiveDependencyGroups))
                        {
                            reader.Skip();
                            break;
                        }
                        break;
                    case JsonTokenType.EndObject:
                        return lockFile;
                    default:
                        throw new JsonException("Unexpected token " + reader.TokenType);
                }
            }

            return lockFile;
        }

        private IList<T> ReadJsonObjectAsArray<T>(Utf8JsonReader reader, JsonConverter<T> jsonConverter, JsonSerializerOptions options)
        {
            reader.Read();
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var listObjects = new List<T>();
                do
                {
                    //We use JsonObjects for the arrays so we advance to the first property in the object which is the name/ver of the first library
                    reader.Read();
                    listObjects.Add(jsonConverter.Read(ref reader, typeof(T), options));
                    //At this point we're looking at the EndObject token for the object, need to advance.
                    reader.Read();
                }
                while (reader.TokenType != JsonTokenType.EndObject);
                return listObjects;
            }
            else
            {
                //If the first token isn't the start of an object then this must be empty or something
                return new List<T>(0);
            }
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

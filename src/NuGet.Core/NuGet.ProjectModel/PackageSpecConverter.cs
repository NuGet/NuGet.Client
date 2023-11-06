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
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="PackageSpec"/>
    /// </summary>
    internal class PackageSpecConverter : JsonConverter<PackageSpec>
    {
        private static readonly byte[] Utf8Authors = Encoding.UTF8.GetBytes("authors");
        private static readonly byte[] BuildOptionsUtf8 = Encoding.UTF8.GetBytes("buildOptions");
        private static readonly byte[] Utf8OutputName = Encoding.UTF8.GetBytes("outputName");
        private static readonly byte[] Utf8ContentFiles = Encoding.UTF8.GetBytes("contentFiles");
        private static readonly byte[] Utf8Copyright = Encoding.UTF8.GetBytes("copyright");
        private static readonly byte[] Utf8Dependencies = Encoding.UTF8.GetBytes("dependencies");
        //private static readonly byte[] Utf8 = Encoding.UTF8.GetBytes("");

        public override PackageSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(PackageSpec))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            var packageSpec = new PackageSpec();
            var stringArrayConverter = (JsonConverter<string[]>)options.GetConverter(typeof(string[]));
            var lockFileLibraryConverter = (JsonConverter<IList<LockFileLibrary>>)options.GetConverter(typeof(IList<LockFileLibrary>));
            var lockFileTargetConverter = (JsonConverter<IList<LockFileTarget>>)options.GetConverter(typeof(IList<LockFileTarget>));
            var projectFileDependencyGroupConverter = (JsonConverter<IList<ProjectFileDependencyGroup>>)options.GetConverter(typeof(IList<ProjectFileDependencyGroup>));
            var lockFileItemListConverter = (JsonConverter<IList<LockFileItem>>)options.GetConverter(typeof(IList<LockFileItem>));

            while (reader.ReadNextToken())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
#pragma warning disable CS0612 // Type or member is obsolete
                        if (reader.ValueTextEquals(Utf8Authors))
                        {
                            reader.ReadNextToken();
                            if (reader.TokenType == JsonTokenType.StartArray)
                            {
                                packageSpec.Authors = stringArrayConverter.Read(ref reader, typeof(string[]), options);
                            }
                            else
                            {
                                packageSpec.Authors = Array.Empty<string>();
                            }
                        }
                        else if (reader.ValueTextEquals(BuildOptionsUtf8))
                        {
                            packageSpec.BuildOptions = new BuildOptions();
                            reader.ReadNextToken();
                            if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                while (reader.TokenType != JsonTokenType.EndObject)
                                {
                                    reader.ReadNextToken();
                                    switch (reader.TokenType)
                                    {
                                        case JsonTokenType.PropertyName:
                                            if (reader.ValueTextEquals(Utf8OutputName))
                                            {
                                                packageSpec.BuildOptions.OutputName = reader.ReadNextTokenAsString();
                                                break;
                                            }
                                            reader.Skip();
                                            break;
                                        default:
                                            //It looks like in the old code it doesn't have any checks for encountering unexpected values when parsing
                                            //the object. Going to just break for now, not sure if reader.Skip() would be better or throwing an exception
                                            break;
                                    }
                                }
                            }
                        }
                        else if (reader.ValueTextEquals(Utf8ContentFiles))
                        {
                            List<string> contentFiles = reader.ReadStringArrayAsList();
                            if (contentFiles != null)
                            {
                                packageSpec.ContentFiles = contentFiles;
                            }
                        }
                        else if (reader.ValueTextEquals(Utf8Copyright))
                        {
                            packageSpec.Copyright = reader.ReadNextTokenAsString();
                        }
#pragma warning restore CS0612 // Type or member is obsolete
                        else if (reader.ValueTextEquals(""))
                        {
                            reader.Skip();
                        }
                        else if (reader.ValueTextEquals(Utf8Dependencies))
                        {
                            reader.Skip();
                        }
                        break;
                    case JsonTokenType.EndObject:
                        return packageSpec;
                    default:
                        throw new JsonException("Unexpected token " + reader.TokenType);
                }
            }

            return packageSpec;
        }

        public override void Write(Utf8JsonWriter writer, PackageSpec value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}

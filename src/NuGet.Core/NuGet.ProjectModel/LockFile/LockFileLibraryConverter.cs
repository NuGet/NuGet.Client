// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="LockFileLibrary"/>
    /// </summary>
    internal class LockFileLibraryConverter : JsonConverter<LockFileLibrary>
    {
        private static readonly byte[] Utf8Sha512 = Encoding.UTF8.GetBytes("sha512");
        private static readonly byte[] Utf8Type = Encoding.UTF8.GetBytes("type");
        private static readonly byte[] Utf8Path = Encoding.UTF8.GetBytes("path");
        private static readonly byte[] Utf8MsbuildProject = Encoding.UTF8.GetBytes("msbuildProject");
        private static readonly byte[] Utf8Servicable = Encoding.UTF8.GetBytes("servicable");
        private static readonly byte[] Utf8HasTools = Encoding.UTF8.GetBytes("hasTools");
        private static readonly byte[] Utf8Files = Encoding.UTF8.GetBytes("files");

        public override LockFileLibrary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(LockFileLibrary))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var lockFileLibrary = new LockFileLibrary();
            //We want to read the property name right away
            var propertyName = reader.GetString();
            var parts = propertyName.Split(new[] { '/' }, 2);
            lockFileLibrary.Name = parts[0];
            if (parts.Length == 2)
            {
                lockFileLibrary.Version = NuGetVersion.Parse(parts[1]);
            }

            var StringListDefaultConverter = (JsonConverter<IList<string>>)options.GetConverter(typeof(IList<string>));
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
                            lockFileLibrary.Type = reader.GetString();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Path))
                        {
                            reader.Read();
                            lockFileLibrary.Path = reader.GetString();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8MsbuildProject))
                        {
                            reader.Read();
                            lockFileLibrary.MSBuildProject = reader.GetString();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Sha512))
                        {
                            reader.Read();
                            lockFileLibrary.Sha512 = reader.GetString();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Servicable))
                        {
                            reader.Read();
                            lockFileLibrary.IsServiceable = reader.GetBoolean();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8HasTools))
                        {
                            reader.Read();
                            lockFileLibrary.HasTools = reader.GetBoolean();
                            break;
                        }
                        if (reader.ValueTextEquals(Utf8Files))
                        {
                            reader.Read();
                            lockFileLibrary.Files = StringListDefaultConverter.Read(ref reader, typeof(IList<string>), options);
                            break;
                        }
                        break;
                    case JsonTokenType.EndObject:
                        return lockFileLibrary;
                    default:
                        throw new JsonException("Unexpected token " + reader.TokenType);
                }
            }
            return lockFileLibrary;
        }

        public override void Write(Utf8JsonWriter writer, LockFileLibrary value, JsonSerializerOptions options)
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

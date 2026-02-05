// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class VersionInfoStjConverter : JsonConverter<VersionInfo>
    {
        public override VersionInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();

            NuGetVersion? nugetVersion = null;
            long? count = null;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propertyName = reader.GetString();

                        switch (propertyName)
                        {
                            case "version":
                                reader.Read();
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    nugetVersion = NuGetVersion.Parse(reader.GetString());
                                }
                                else
                                {
                                    throw new JsonException("version should be a string");
                                }
                                break;

                            case "downloads":
                                reader.Read();

                                if (reader.TokenType == JsonTokenType.Number)
                                {
                                    count = reader.GetInt64();
                                }
                                else
                                {
                                    throw new JsonException("downloads should be a number");
                                }
                                break;

                            default:
                                reader.Skip();
                                break;
                        }
                        break;

                    case JsonTokenType.EndObject:
                        return new VersionInfo(nugetVersion, count);

                    default:
                        throw new JsonException("Unexpected token: " + reader.TokenType);
                }
            }

            return new VersionInfo(nugetVersion, count);
        }

        public override void Write(Utf8JsonWriter writer, VersionInfo value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}

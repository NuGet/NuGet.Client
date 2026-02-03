// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NuGet.Protocol.Model;

namespace NuGet.Protocol.Converters
{
    internal class V3SearchResultsStjConverter : JsonConverter<V3SearchResults>
    {
        private readonly uint _take;
        private readonly JsonSerializerOptions _options;

        public V3SearchResultsStjConverter(uint take, JsonSerializerOptions options)
        {
            _take = take;
            _options = options;
        }

        public override V3SearchResults Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            var searchResults = new V3SearchResults();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propertyName = reader.GetString();

                        switch (propertyName)
                        {
                            case "totalHits":
                                reader.Read();
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    if (long.TryParse(reader.GetString(), out var totalHits))
                                    {
                                        searchResults.TotalHits = totalHits;
                                    }
                                    else
                                    {
                                        throw new JsonException("totalHits should be a long integer");
                                    }
                                }
                                else if (reader.TokenType == JsonTokenType.Number)
                                {
                                    searchResults.TotalHits = reader.GetInt64();
                                }
                                else
                                {
                                    throw new JsonException("totalHits should be a number");
                                }
                                break;

                            case "data":
                                reader.Read();

                                if (reader.TokenType != JsonTokenType.StartArray)
                                {
                                    throw new JsonException("data should be an array");
                                }

                                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                                {
                                    JsonTypeInfo<PackageSearchMetadata>? typeInfo = _options.GetTypeInfo(typeof(PackageSearchMetadata)) as System.Text.Json.Serialization.Metadata.JsonTypeInfo<PackageSearchMetadata>;
                                    if (typeInfo == null)
                                    {
                                        reader.Skip();
                                        continue;
                                    }

                                    PackageSearchMetadata? searchResult = JsonSerializer.Deserialize(ref reader, typeInfo);

                                    if (searchResult == null)
                                    {
                                        continue;
                                    }

                                    searchResults.Data.Add(searchResult);

                                    if (searchResults.Data.Count >= _take)
                                    {
                                        // Skip remaining array items
                                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                                        {
                                            reader.Skip();
                                        }
                                        break;
                                    }
                                }

                                break;

                            default:
                                reader.Skip();
                                break;
                        }
                        break;

                    case JsonTokenType.EndObject:
                        return searchResults;

                    default:
                        throw new JsonException("Unexpected token: " + reader.TokenType);
                }
            }

            return searchResults;
        }

        public override void Write(Utf8JsonWriter writer, V3SearchResults value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}

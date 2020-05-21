using Newtonsoft.Json;
using NuGet.Protocol.Model;
using System;

namespace NuGet.Protocol.Converters
{
    public class V3SearchResultsConverter : JsonConverter
    {
        public int Take { get; set; }

        public V3SearchResultsConverter(int take)
        {
            Take = take;
        }

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => objectType == typeof(V3SearchResults);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType != typeof(V3SearchResults))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            var searchResults = new V3SearchResults();

            if (Take <= 0)
            {
                return searchResults;
            }

            var finished = false;

            while (!finished)
            {
                reader.Read();

                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        switch ((string)reader.Value)
                        {
                            case "totalHits":
                                long totalHits = long.TryParse(reader.ReadAsString(), out totalHits) ? totalHits : 0;
                                searchResults.TotalHits = totalHits;
                                break;

                            case "data":
                                reader.Read();

                                if (reader.TokenType != JsonToken.StartArray)
                                {
                                    throw new JsonException("data should be an array");
                                }

                                reader.Read();

                                while (reader.TokenType != JsonToken.EndArray)
                                {
                                    var searchResult = JsonExtensions.JsonObjectSerializer.Deserialize<PackageSearchMetadata>(reader);

                                    searchResults.Data.Add(searchResult);
                                    Take--;

                                    if (Take <= 0)
                                    {
                                        finished = true;
                                        break;
                                    }

                                    reader.Read();
                                }

                                break;

                            default:
                                reader.Skip();
                                break;
                        }
                        break;

                    case JsonToken.EndObject:
                        finished = true;
                        break;

                    default:
                        throw new JsonException();
                }
            }

            return searchResults;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace NuGet.Protocol.Core.v3
{
    public class MetadataFieldConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => (objectType == typeof(string));

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return string.Empty;
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);
                return string.Join(", ", array.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            return serializer.Deserialize<string>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}

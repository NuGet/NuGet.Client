using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    public class VersionInfoConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(VersionInfo);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var v = JObject.Load(reader);
            var nugetVersion = NuGetVersion.Parse(v.Value<string>("version"));
            var count = v.Value<int?>("downloads");
            return new VersionInfo(nugetVersion, count);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}

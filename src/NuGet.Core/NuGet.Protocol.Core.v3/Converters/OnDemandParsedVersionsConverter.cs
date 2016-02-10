using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using System;

namespace NuGet.Protocol.Core.v3
{
    public class OnDemandParsedVersionsConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(VersionInfo[]);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);
            return new Lazy<VersionInfo[]>(() => ParseVersionArray(array));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private static VersionInfo[] ParseVersionArray(JArray versionArray)
        {
            return versionArray?.FromJToken<VersionInfo[]>() ?? new VersionInfo[] { };
        }
    }
}

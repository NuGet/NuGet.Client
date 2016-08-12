using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class PackageDependencyGroupConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => (objectType == typeof(PackageDependencyGroup));

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var set = JObject.Load(reader);

            var fxName = set.Value<string>(JsonProperties.TargetFramework);

            var framework = NuGetFramework.AnyFramework;

            if (!string.IsNullOrEmpty(fxName))
            {
                framework = NuGetFramework.Parse(fxName);
                fxName = framework.GetShortFolderName();
            }

            var packages = (set[JsonProperties.Dependencies] as JArray ?? Enumerable.Empty<JToken>())
                .Select(LoadDependency);
            return new PackageDependencyGroup(framework, new HashSet<PackageDependency>(packages));
        }

        private static Packaging.Core.PackageDependency LoadDependency(JToken dependency)
        {
            var ver = dependency.Value<string>(JsonProperties.Range);
            return new Packaging.Core.PackageDependency(
                dependency.Value<string>(JsonProperties.PackageId),
                string.IsNullOrEmpty(ver) ? null : VersionRange.Parse(ver));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}

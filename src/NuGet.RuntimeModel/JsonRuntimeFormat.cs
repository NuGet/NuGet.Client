using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Versioning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.RuntimeModel
{
    public static class JsonRuntimeFormat
    {
        public static RuntimeGraph ReadRuntimeGraph(string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                return ReadRuntimeGraph(fileStream);
            }
        }

        public static RuntimeGraph ReadRuntimeGraph(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                return ReadRuntimeGraph(streamReader);
            }
        }

        public static RuntimeGraph ReadRuntimeGraph(TextReader textReader)
        {
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return ReadRuntimeGraph(JToken.Load(jsonReader));
            }
        }

        public static void WriteRuntimeGraph(string filePath, RuntimeGraph runtimeGraph)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var textWriter = new StreamWriter(fileStream))
                {
                    using (var jsonWriter = new JsonTextWriter(textWriter))
                    {
                        jsonWriter.Formatting = Formatting.Indented;
                        var json = new JObject();
                        WriteRuntimeGraph(json, runtimeGraph);
                        json.WriteTo(jsonWriter);
                    }
                }
            }
        }

        public static RuntimeGraph ReadRuntimeGraph(JToken json)
        {
            var graph = new RuntimeGraph();
            foreach (var runtimeSpec in EachProperty(json["runtimes"]).Select(ReadRuntimeDescription))
            {
                graph.Runtimes.Add(runtimeSpec.RuntimeIdentifier, runtimeSpec);
            }
            return graph;
        }

        private static void WriteRuntimeGraph(JObject json, RuntimeGraph runtimeGraph)
        {
            var runtimes = new JObject();
            json["runtimes"] = runtimes;
            foreach (var x in runtimeGraph.Runtimes.Values)
            {
                WriteRuntimeDescription(runtimes, x);
            }
        }

        private static void WriteRuntimeDescription(JObject json, RuntimeDescription data)
        {
            var value = new JObject();
            json[data.RuntimeIdentifier] = value;
            value["#import"] = new JArray(data.InheritedRuntimes.Select(x => new JValue(x)));
            foreach (var x in data.RuntimeDependencySets.Values)
            {
                WriteRuntimeDependencySet(value, x);
            }
        }

        private static void WriteRuntimeDependencySet(JObject json, RuntimeDependencySet data)
        {
            var value = new JObject();
            json[data.Id] = value;
            foreach (var x in data.Dependencies.Values)
            {
                WritePackageDependency(value, x);
            }
        }

        private static void WritePackageDependency(JObject json, RuntimePackageDependency data)
        {
            json[data.Id] = new JValue(data.VersionRange.ToNormalizedString());
        }

        private static RuntimeDescription ReadRuntimeDescription(KeyValuePair<string, JToken> json)
        {
            string name = json.Key;
            IList<string> inheritedRuntimes = new List<string>();
            IList<RuntimeDependencySet> additionalDependencies = new List<RuntimeDependencySet>();
            foreach (var property in EachProperty(json.Value))
            {
                if (property.Key == "#import")
                {
                    var imports = property.Value as JArray;
                    foreach (var import in imports)
                    {
                        inheritedRuntimes.Add(import.Value<string>());
                    }
                }
                else
                {
                    var dependency = ReadRuntimeDependencySet(property);
                    additionalDependencies.Add(dependency);
                }
            }
            return new RuntimeDescription(name, inheritedRuntimes, additionalDependencies);
        }

        private static RuntimeDependencySet ReadRuntimeDependencySet(KeyValuePair<string, JToken> json)
        {
            return new RuntimeDependencySet(
                json.Key,
                EachProperty(json.Value).Select(ReadRuntimePackageDependency));
        }

        private static RuntimePackageDependency ReadRuntimePackageDependency(KeyValuePair<string, JToken> json)
        {
            return new RuntimePackageDependency(json.Key, VersionRange.Parse(json.Value.Value<string>()));
        }

        private static IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                ?? Enumerable.Empty<KeyValuePair<string, JToken>>();
        }

        private static IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json, string defaultPropertyName)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                ?? new[] { new KeyValuePair<string, JToken>(defaultPropertyName, json) };
        }

        private static IEnumerable<JToken> EachArray(JToken json)
        {
            return (IEnumerable<JToken>)(json as JArray)
                ?? new[] { json };
        }

    }
}
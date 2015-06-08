// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

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
            return new RuntimeGraph(
                EachProperty(json["runtimes"]).Select(ReadRuntimeDescription),
                EachProperty(json["supports"]).Select(ReadCompatibilityProfile));
        }

        private static void WriteRuntimeGraph(JObject jObject, RuntimeGraph runtimeGraph)
        {
            if (runtimeGraph.Runtimes.Any())
            {
                var runtimes = new JObject();
                jObject["runtimes"] = runtimes;
                foreach (var x in runtimeGraph.Runtimes.Values)
                {
                    WriteRuntimeDescription(runtimes, x);
                }
            }

            if (runtimeGraph.Supports.Any())
            {
                var supports = new JObject();
                jObject["supports"] = supports;
                foreach(var x in runtimeGraph.Supports.Values)
                {
                    WriteCompatibilityProfile(supports, x);
                }
            }
        }

        private static void WriteRuntimeDescription(JObject jObject, RuntimeDescription data)
        {
            var value = new JObject();
            jObject[data.RuntimeIdentifier] = value;
            value["#import"] = new JArray(data.InheritedRuntimes.Select(x => new JValue(x)));
            foreach (var x in data.RuntimeDependencySets.Values)
            {
                WriteRuntimeDependencySet(value, x);
            }
        }

        private static void WriteRuntimeDependencySet(JObject jObject, RuntimeDependencySet data)
        {
            var value = new JObject();
            jObject[data.Id] = value;
            foreach (var x in data.Dependencies.Values)
            {
                WritePackageDependency(value, x);
            }
        }

        private static void WritePackageDependency(JObject jObject, RuntimePackageDependency data)
        {
            jObject[data.Id] = new JValue(data.VersionRange.ToNormalizedString());
        }

        private static void WriteCompatibilityProfile(JObject jObject, CompatibilityProfile data)
        {
            var value = new JObject();
            jObject[data.Name] = value;
            foreach(var frameworkGroup in data.RestoreContexts.GroupBy(f => f.Framework))
            {
                var name = frameworkGroup.Key.GetShortFolderName();
                var runtimes = frameworkGroup.ToList();
                if(runtimes.Count == 1)
                {
                    // Write a string
                    value[name] = runtimes[0].RuntimeIdentifier;
                }
                else if (runtimes.Count > 0)
                {
                    var array = new JArray();
                    value[name] = array;
                    foreach(var runtime in runtimes)
                    {
                        array.Add(runtime.RuntimeIdentifier);
                    }
                }
            }
        }

        private static CompatibilityProfile ReadCompatibilityProfile(KeyValuePair<string, JToken> json)
        {
            var name = json.Key;
            var sets = new List<FrameworkRuntimePair>();
            foreach(var property in EachProperty(json.Value))
            {
                var profiles = ReadCompatibilitySets(property);
                sets.AddRange(profiles);
            }
            return new CompatibilityProfile(name, sets);
        }

        private static IEnumerable<FrameworkRuntimePair> ReadCompatibilitySets(KeyValuePair<string, JToken> property)
        {
            var framework = NuGetFramework.Parse(property.Key);
            switch(property.Value.Type)
            {
                case JTokenType.Array:
                    foreach(var value in (JArray)property.Value)
                    {
                        yield return new FrameworkRuntimePair(framework, value.Value<string>());
                    }
                    break;
                case JTokenType.String:
                    yield return new FrameworkRuntimePair(framework, property.Value.ToString());
                    break;
                // Other token types are not supported
            }
        }

        private static RuntimeDescription ReadRuntimeDescription(KeyValuePair<string, JToken> json)
        {
            var name = json.Key;
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

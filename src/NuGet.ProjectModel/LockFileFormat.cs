// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileFormat
    {
        public const string LockFileName = "project.lock.json";

        public LockFile Read(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(stream);
            }
        }

        public LockFile Read(Stream stream)
        {
            using (var textReader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    while (jsonReader.TokenType != JsonToken.StartObject)
                    {
                        if (!jsonReader.Read())
                        {
                            //TODO: throw exception
                            return null;
                        }
                    }
                    var token = JToken.Load(jsonReader);
                    return ReadLockFile(token as JObject);
                }
            }
        }

        public void Write(string filePath, LockFile lockFile)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, lockFile);
            }
        }

        public void Write(Stream stream, LockFile lockFile)
        {
            using (var textWriter = new StreamWriter(stream))
            {
                using (var jsonWriter = new JsonTextWriter(textWriter))
                {
                    jsonWriter.Formatting = Formatting.Indented;

                    var json = WriteLockFile(lockFile);
                    json.WriteTo(jsonWriter);
                }
            }
        }

        private LockFile ReadLockFile(JObject cursor)
        {
            var lockFile = new LockFile();
            lockFile.Islocked = ReadBool(cursor, "locked", defaultValue: false);
            lockFile.Libraries = ReadObject(cursor["libraries"] as JObject, ReadLibrary);
            return lockFile;
        }

        private JObject WriteLockFile(LockFile lockFile)
        {
            var json = new JObject();
            json["locked"] = new JValue(lockFile.Islocked);
            json["version"] = new JValue(1);
            json["libraries"] = WriteObject(lockFile.Libraries, WriteLibrary);
            return json;
        }

        private LockFileLibrary ReadLibrary(string property, JToken json)
        {
            var library = new LockFileLibrary();
            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = NuGetVersion.Parse(parts[1]);
            }
            library.DependencyGroups = ReadObject(json["dependencySets"] as JObject, ReadPackageDependencySet);
            library.FrameworkReferenceGroups = ReadFrameworkAssemblies(json["frameworkAssemblies"] as JObject);
            library.ReferenceGroups = ReadArray(json["packageAssemblyReferences"] as JArray, ReadPackageReferenceSet);
            library.Files = ReadObject(json["contents"] as JObject, ReadPackageFile);
            return library;
        }

        private JProperty WriteLibrary(LockFileLibrary library)
        {
            var json = new JObject();
            WriteObject(json, "dependencySets", library.DependencyGroups, WritePackageDependencySet);
            WriteFrameworkAssemblies(json, "frameworkAssemblies", library.FrameworkReferenceGroups);
            WriteArray(json, "packageAssemblyReferences", library.ReferenceGroups, WritePackageReferenceSet);
            json["contents"] = WriteObject(library.Files, WritePackageFile);
            return new JProperty(
                library.Name + "/" + library.Version.ToString(),
                json);
        }

        private IList<FrameworkSpecificGroup> ReadFrameworkAssemblies(JObject json)
        {
            var frameworkSets = ReadObject(json, (property, child) => new
            {
                FrameworkName = property,
                AssemblyNames = ReadArray(child as JArray, ReadString)
            });

            return frameworkSets.Select(frameworkSet =>
            {
                if (frameworkSet.FrameworkName == "*")
                {
                    return new FrameworkSpecificGroup(NuGetFramework.AnyFramework, frameworkSet.AssemblyNames);
                }
                else
                {
                    return new FrameworkSpecificGroup(frameworkSet.FrameworkName, frameworkSet.AssemblyNames);
                }
            }).ToList();
        }

        private void WriteFrameworkAssemblies(JToken json, string property, IList<FrameworkSpecificGroup> frameworkAssemblies)
        {
            if (frameworkAssemblies.Any())
            {
                json[property] = WriteFrameworkAssemblies(frameworkAssemblies);
            }
        }

        private JToken WriteFrameworkAssemblies(IList<FrameworkSpecificGroup> groups)
        {
            return WriteObject(groups, group =>
            {
                return new JProperty(group.TargetFramework?.ToString() ?? "*", group.Items.Select(x => new JValue(x)));
            });
        }

        private PackageDependencyGroup ReadPackageDependencySet(string property, JToken json)
        {
            var targetFramework = string.Equals(property, "*") ? null : property;
            return new PackageDependencyGroup(
                targetFramework,
                ReadObject(json as JObject, ReadPackageDependency));
        }

        private JProperty WritePackageDependencySet(PackageDependencyGroup item)
        {
            return new JProperty(
                item.TargetFramework?.ToString() ?? "*",
                WriteObject(item.Packages, WritePackageDependency));
        }


        private PackageDependency ReadPackageDependency(string property, JToken json)
        {
            return new PackageDependency(
                property,
                VersionRange.Parse(json.Value<string>()));
        }

        private JProperty WritePackageDependency(PackageDependency item)
        {
            string versionRange = null;

            if (item.VersionRange != null)
            {
                if (item.VersionRange.IsMinInclusive && item.VersionRange.MaxVersion == null)
                {
                    versionRange = item.VersionRange.MinVersion.ToString();
                }
                else
                {
                    versionRange = item.VersionRange.ToString();
                }
            }

            return new JProperty(
                item.Id,
                WriteString(versionRange));
        }

        private IEnumerable<FrameworkSpecificGroup> ReadFrameworkAssemblyReference(string property, JToken json)
        {
            var supportedFrameworks = ReadArray(json["supportedFrameworks"] as JArray, ReadFrameworkName);
            if (supportedFrameworks != null && supportedFrameworks.Any())
            {
                return supportedFrameworks
                    .Select(x => new FrameworkSpecificGroup(property, new[] { x.ToString() }))
                    .ToList();
            }
            return new[] { new FrameworkSpecificGroup(property, Enumerable.Empty<string>()) };
        }

        private FrameworkSpecificGroup ReadPackageReferenceSet(JToken json)
        {
            throw new NotImplementedException();
        }

        private JToken WritePackageReferenceSet(FrameworkSpecificGroup item)
        {
            var json = new JObject();
            json["targetFramework"] = item.TargetFramework?.ToString();
            json["references"] = WriteArray(item.Items, WriteString);
            return json;
        }

        private string ReadPackageFile(string property, JToken json)
        {
            return property;
        }

        private JProperty WritePackageFile(string path)
        {
            var json = new JObject();
            return new JProperty(path, new JObject());
        }

        private IList<TItem> ReadArray<TItem>(JArray json, Func<JToken, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var child in json)
            {
                items.Add(readItem(child));
            }
            return items;
        }

        private void WriteArray<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteArray(items, writeItem);
            }
        }

        private JArray WriteArray<TItem>(IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            var array = new JArray();
            foreach (var item in items)
            {
                array.Add(writeItem(item));
            }
            return array;
        }

        private IList<TItem> ReadObject<TItem>(JObject json, Func<string, JToken, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var child in json)
            {
                items.Add(readItem(child.Key, child.Value));
            }
            return items;
        }

        private void WriteObject<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JProperty> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteObject(items, writeItem);
            }
        }

        private JObject WriteObject<TItem>(IEnumerable<TItem> items, Func<TItem, JProperty> writeItem)
        {
            var array = new JObject();
            foreach (var item in items)
            {
                array.Add(writeItem(item));
            }
            return array;
        }

        private bool ReadBool(JToken cursor, string property, bool defaultValue)
        {
            var valueToken = cursor[property];
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value<bool>();
        }

        private string ReadString(JToken json)
        {
            return json.Value<string>();
        }

        private NuGetVersion ReadSemanticVersion(JToken json, string property)
        {
            var valueToken = json[property];
            if (valueToken == null)
            {
                throw new Exception(string.Format("TODO: lock file missing required property {0}", property));
            }
            return NuGetVersion.Parse(valueToken.Value<string>());
        }

        private void WriteBool(JToken token, string property, bool value)
        {
            token[property] = new JValue(value);
        }

        private JToken WriteString(string item)
        {
            return item != null ? new JValue(item) : JValue.CreateNull();
        }

        private NuGetFramework ReadFrameworkName(JToken json)
        {
            return json == null ? null : NuGetFramework.Parse(json.Value<string>());
        }
        private JToken WriteFrameworkName(FrameworkName item)
        {
            return item != null ? new JValue(item.ToString()) : JValue.CreateNull();
        }
    }
}
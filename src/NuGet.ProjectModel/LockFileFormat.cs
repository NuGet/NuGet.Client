// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
        public const int Version = -9997;
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
                try
                {
                    using (var jsonReader = new JsonTextReader(textReader))
                    {
                        while (jsonReader.TokenType != JsonToken.StartObject)
                        {
                            if (!jsonReader.Read())
                            {
                                throw new InvalidDataException();
                            }
                        }
                        var token = JToken.Load(jsonReader);
                        return ReadLockFile(token as JObject);
                    }
                }
                catch
                {
                    // Ran into parsing errors, mark it as unlocked and out-of-date
                    return new LockFile
                    {
                        IsLocked = false,
                        Version = int.MinValue
                    };
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
            lockFile.IsLocked = ReadBool(cursor, "locked", defaultValue: false);
            lockFile.Version = ReadInt(cursor, "version", defaultValue: int.MinValue);
            lockFile.Libraries = ReadObject(cursor["libraries"] as JObject, ReadLibrary);
            lockFile.Targets = ReadObject(cursor["targets"] as JObject, ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor["projectFileDependencyGroups"] as JObject, ReadProjectFileDependencyGroup);
            return lockFile;
        }

        private JObject WriteLockFile(LockFile lockFile)
        {
            var json = new JObject();
            json["locked"] = new JValue(lockFile.IsLocked);
            json["version"] = new JValue(Version);
            json["targets"] = WriteObject(lockFile.Targets, WriteTarget);
            json["libraries"] = WriteObject(lockFile.Libraries, WriteLibrary);
            json["projectFileDependencyGroups"] = WriteObject(lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup);
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
            library.IsServiceable = ReadBool(json, "serviceable", defaultValue: false);
            library.Sha512 = ReadString(json["sha512"]);
            library.Files = ReadPathArray(json["files"] as JArray, ReadString);
            return library;
        }

        private JProperty WriteLibrary(LockFileLibrary library)
        {
            var json = new JObject();
            if (library.IsServiceable)
            {
                WriteBool(json, "serviceable", library.IsServiceable);
            }
            json["sha512"] = WriteString(library.Sha512);
            WritePathArray(json, "files", library.Files, WriteString);
            return new JProperty(
                library.Name + "/" + library.Version.ToString(),
                json);
        }

        private JProperty WriteTarget(LockFileTarget target)
        {
            var json = WriteObject(target.Libraries, WriteTargetLibrary);

            var key = target.TargetFramework + (string.IsNullOrEmpty(target.RuntimeIdentifier) ? "" : "/" + target.RuntimeIdentifier);

            return new JProperty(key, json);
        }

        private LockFileTarget ReadTarget(string property, JToken json)
        {
            var target = new LockFileTarget();
            var parts = property.Split(new[] { '/' }, 2);
            target.TargetFramework = NuGetFramework.Parse(parts[0]);
            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = parts[1];
            }

            target.Libraries = ReadObject(json as JObject, ReadTargetLibrary);

            return target;
        }

        private LockFileTargetLibrary ReadTargetLibrary(string property, JToken json)
        {
            var library = new LockFileTargetLibrary();

            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = NuGetVersion.Parse(parts[1]);
            }

            library.Dependencies = ReadObject(json["dependencies"] as JObject, ReadPackageDependency);
            library.FrameworkAssemblies = ReadArray(json["frameworkAssemblies"] as JArray, ReadString);
            library.RuntimeAssemblies = ReadPathArray(json["runtime"] as JArray, ReadString);
            library.CompileTimeAssemblies = ReadPathArray(json["compile"] as JArray, ReadString);
            library.NativeLibraries = ReadPathArray(json["native"] as JArray, ReadString);

            return library;
        }

        private JProperty WriteTargetLibrary(LockFileTargetLibrary library)
        {
            var json = new JObject();

            if (library.Dependencies.Count > 0)
            {
                json["dependencies"] = WriteObject(library.Dependencies, WritePackageDependency);
            }

            if (library.FrameworkAssemblies.Count > 0)
            {
                json["frameworkAssemblies"] = WriteArray(library.FrameworkAssemblies, WriteString);
            }

            if (library.CompileTimeAssemblies.Count > 0)
            {
                json["compile"] = WritePathArray(library.CompileTimeAssemblies, WriteString);
            }

            if (library.RuntimeAssemblies.Count > 0)
            {
                json["runtime"] = WritePathArray(library.RuntimeAssemblies, WriteString);
            }

            if (library.NativeLibraries.Count > 0)
            {
                json["native"] = WritePathArray(library.NativeLibraries, WriteString);
            }

            return new JProperty(library.Name + "/" + library.Version, json);
        }


        private LockFileFrameworkGroup ReadFrameworkGroup(string property, JToken json)
        {
            var group = new LockFileFrameworkGroup();

            group.TargetFramework = NuGetFramework.Parse(property);
            group.Dependencies = ReadObject(json["dependencies"] as JObject, ReadPackageDependency);
            group.FrameworkAssemblies = ReadArray(json["frameworkAssemblies"] as JArray, ReadString);
            group.RuntimeAssemblies = ReadPathArray(json["runtimeAssemblies"] as JArray, ReadString);
            group.CompileTimeAssemblies = ReadPathArray(json["compileAssemblies"] as JArray, ReadString);

            return group;
        }

        private JProperty WriteFrameworkGroup(LockFileFrameworkGroup group)
        {
            var json = new JObject();
            json["dependencies"] = WriteObject(group.Dependencies, WritePackageDependency);
            json["frameworkAssemblies"] = WriteArray(group.FrameworkAssemblies, WriteString);
            json["runtimeAssemblies"] = WritePathArray(group.RuntimeAssemblies, WriteString);
            json["compileAssemblies"] = WritePathArray(group.CompileTimeAssemblies, WriteString);

            return new JProperty(group.TargetFramework.DotNetFrameworkName, json);
        }

        private ProjectFileDependencyGroup ReadProjectFileDependencyGroup(string property, JToken json)
        {
            return new ProjectFileDependencyGroup(
                property,
                ReadArray(json as JArray, ReadString));
        }

        private JProperty WriteProjectFileDependencyGroup(ProjectFileDependencyGroup frameworkInfo)
        {
            return new JProperty(
                frameworkInfo.FrameworkName,
                WriteArray(frameworkInfo.Dependencies, WriteString));
        }

        private PackageDependencyGroup ReadPackageDependencyGroup(string property, JToken json)
        {
            var targetFramework = string.Equals(property, "*") ? null : new NuGetFramework(property);
            return new PackageDependencyGroup(
                targetFramework,
                ReadObject(json as JObject, ReadPackageDependency));
        }

        private JProperty WritePackageDependencyGroup(PackageDependencyGroup item)
        {
            return new JProperty(
                item.TargetFramework?.ToString() ?? "*",
                WriteObject(item.Packages, WritePackageDependency));
        }


        private PackageDependency ReadPackageDependency(string property, JToken json)
        {
            var versionStr = json.Value<string>();
            return new PackageDependency(
                property,
                versionStr == null ? null : VersionRange.Parse(versionStr));
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

        private IList<string> ReadPathArray(JArray json, Func<JToken, string> readItem)
        {
            return ReadArray(json, readItem).Select(f => GetPathWithDirectorySeparator(f)).ToList();
        }

        private void WriteArray<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteArray(items, writeItem);
            }
        }

        private void WritePathArray(JToken json, string property, IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            WriteArray(json, property, items.Select(f => GetPathWithForwardSlashes(f)), writeItem);
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

        private JArray WritePathArray(IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            return WriteArray(items.Select(f => GetPathWithForwardSlashes(f)), writeItem);
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

        private int ReadInt(JToken cursor, string property, int defaultValue)
        {
            var valueToken = cursor[property];
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value<int>();
        }

        private string ReadString(JToken json)
        {
            return json.Value<string>();
        }

        private SemanticVersion ReadSemanticVersion(JToken json, string property)
        {
            var valueToken = json[property];
            if (valueToken == null)
            {
                throw new Exception(string.Format("TODO: lock file missing required property {0}", property));
            }
            return SemanticVersion.Parse(valueToken.Value<string>());
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
            return json == null ? null : new NuGetFramework(json.Value<string>());
        }
        private JToken WriteFrameworkName(NuGetFramework item)
        {
            return item != null ? new JValue(item.ToString()) : JValue.CreateNull();
        }

        private static string GetPathWithForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string GetPathWithBackSlashes(string path)
        {
            return path.Replace('/', '\\');
        }

        private static string GetPathWithDirectorySeparator(string path)
        {
            if (Path.DirectorySeparatorChar == '/')
            {
                return GetPathWithForwardSlashes(path);
            }
            else
            {
                return GetPathWithBackSlashes(path);
            }
        }

    }
}
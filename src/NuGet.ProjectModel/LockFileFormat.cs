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
        public static readonly int Version = -9996;
        public static readonly string LockFileName = "project.lock.json";

        private static readonly char[] PathSplitChars = new[] { '/' };

        private const string LockedProperty = "locked";
        private const string VersionProperty = "version";
        private const string LibrariesProperty = "libraries";
        private const string TargetsProperty = "targets";
        private const string ProjectFileDependencyGroupsProperty = "projectFileDependencyGroups";
        private const string ServicableProperty = "servicable";
        private const string Sha512Property = "sha512";
        private const string FilesProperty = "files";
        private const string DependenciesProperty = "dependencies";
        private const string FrameworkAssembliesProperty = "frameworkAssemblies";
        private const string RuntimeProperty = "runtime";
        private const string CompileProperty = "compile";
        private const string NativeProperty = "native";
        private const string ResourceProperty = "resource";

        // Legacy property names
        private const string RuntimeAssembliesProperty = "runtimeAssemblies";
        private const string CompileAssembliesProperty = "compileAssemblies";

        public LockFile Read(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
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
            lockFile.IsLocked = ReadBool(cursor, LockedProperty, defaultValue: false);
            lockFile.Version = ReadInt(cursor, VersionProperty, defaultValue: int.MinValue);
            lockFile.Libraries = ReadObject(cursor[LibrariesProperty] as JObject, ReadLibrary);
            lockFile.Targets = ReadObject(cursor[TargetsProperty] as JObject, ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor[ProjectFileDependencyGroupsProperty] as JObject, ReadProjectFileDependencyGroup);
            return lockFile;
        }

        private JObject WriteLockFile(LockFile lockFile)
        {
            var json = new JObject();
            json[LockedProperty] = new JValue(lockFile.IsLocked);
            json[VersionProperty] = new JValue(Version);
            json[TargetsProperty] = WriteObject(lockFile.Targets, WriteTarget);
            json[LibrariesProperty] = WriteObject(lockFile.Libraries, WriteLibrary);
            json[ProjectFileDependencyGroupsProperty] = WriteObject(lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup);
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
            library.IsServiceable = ReadBool(json, ServicableProperty, defaultValue: false);
            library.Sha512 = ReadString(json[Sha512Property]);
            library.Files = ReadPathArray(json[FilesProperty] as JArray, ReadString);
            return library;
        }

        private JProperty WriteLibrary(LockFileLibrary library)
        {
            var json = new JObject();
            if (library.IsServiceable)
            {
                WriteBool(json, ServicableProperty, library.IsServiceable);
            }
            json[Sha512Property] = WriteString(library.Sha512);
            WritePathArray(json, FilesProperty, library.Files, WriteString);
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
            var parts = property.Split(PathSplitChars, 2);
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

            library.Dependencies = ReadObject(json[DependenciesProperty] as JObject, ReadPackageDependency);
            library.FrameworkAssemblies = ReadArray(json[FrameworkAssembliesProperty] as JArray, ReadString);
            library.RuntimeAssemblies = ReadObject(json[RuntimeProperty] as JObject, ReadFileItem);
            library.CompileTimeAssemblies = ReadObject(json[CompileProperty] as JObject, ReadFileItem);
            library.ResourceAssemblies = ReadObject(json[ResourceProperty] as JObject, ReadFileItem);
            library.NativeLibraries = ReadObject(json[NativeProperty] as JObject, ReadFileItem);

            return library;
        }

        private JProperty WriteTargetLibrary(LockFileTargetLibrary library)
        {
            var json = new JObject();

            if (library.Dependencies.Count > 0)
            {
                json[DependenciesProperty] = WriteObject(library.Dependencies, WritePackageDependency);
            }

            if (library.FrameworkAssemblies.Count > 0)
            {
                json[FrameworkAssembliesProperty] = WriteArray(library.FrameworkAssemblies, WriteString);
            }

            if (library.CompileTimeAssemblies.Count > 0)
            {
                json[CompileProperty] = WriteObject(library.CompileTimeAssemblies, WriteFileItem);
            }

            if (library.RuntimeAssemblies.Count > 0)
            {
                json[RuntimeProperty] = WriteObject(library.RuntimeAssemblies, WriteFileItem);
            }

            if (library.ResourceAssemblies.Count > 0)
            {
                json[ResourceProperty] = WriteObject(library.ResourceAssemblies, WriteFileItem);
            }

            if (library.NativeLibraries.Count > 0)
            {
                json[NativeProperty] = WriteObject(library.NativeLibraries, WriteFileItem);
            }

            return new JProperty(library.Name + "/" + library.Version.ToNormalizedString(), json);
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

        private LockFileItem ReadFileItem(string property, JToken json)
        {
            var item = new LockFileItem { Path = property };
            foreach (var subProperty in json.OfType<JProperty>())
            {
                item.Properties[subProperty.Name] = subProperty.Value.Value<string>();
            }
            return item;
        }

        private JProperty WriteFileItem(LockFileItem item)
        {
            return new JProperty(
                item.Path,
               new JObject(item.Properties.Select(x => new JProperty(x.Key, x.Value))));
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
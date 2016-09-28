// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileFormat
    {
        public static readonly int Version = 2;
        public static readonly string LockFileName = "project.lock.json";

        private static readonly char[] PathSplitChars = new[] { LockFile.DirectorySeparatorChar };

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
        private const string ContentFilesProperty = "contentFiles";
        private const string RuntimeTargetsProperty = "runtimeTargets";
        private const string ResourceProperty = "resource";
        private const string TypeProperty = "type";
        private const string PathProperty = "path";
        private const string MSBuildProjectProperty = "msbuildProject";
        private const string FrameworkProperty = "framework";
        private const string ToolsProperty = "tools";
        private const string ProjectFileToolGroupsProperty = "projectFileToolGroups";
        private const string PackageFoldersProperty = "packageFolders";

        // Legacy property names
        private const string RuntimeAssembliesProperty = "runtimeAssemblies";
        private const string CompileAssembliesProperty = "compileAssemblies";

        public LockFile Parse(string lockFileContent, string path)
        {
            return Parse(lockFileContent, NullLogger.Instance, path);
        }

        public LockFile Parse(string lockFileContent, ILogger log, string path)
        {
            using (var reader = new StringReader(lockFileContent))
            {
                return Read(reader, log, path);
            }
        }

        public LockFile Read(string filePath)
        {
            return Read(filePath, NullLogger.Instance);
        }

        public LockFile Read(string filePath, ILogger log)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Read(stream, log, filePath);
            }
        }

        public LockFile Read(Stream stream, string path)
        {
            return Read(stream, NullLogger.Instance, path);
        }

        public LockFile Read(Stream stream, ILogger log, string path)
        {
            using (var textReader = new StreamReader(stream))
            {
                return Read(textReader, log, path);
            }
        }

        public LockFile Read(TextReader reader, string path)
        {
            return Read(reader, NullLogger.Instance, path);
        }

        public LockFile Read(TextReader reader, ILogger log, string path)
        {
            try
            {
                using (var jsonReader = new JsonTextReader(reader))
                {
                    while (jsonReader.TokenType != JsonToken.StartObject)
                    {
                        if (!jsonReader.Read())
                        {
                            throw new InvalidDataException();
                        }
                    }
                    var token = JToken.Load(jsonReader);
                    var lockFile = ReadLockFile(token as JObject);
                    lockFile.Path = path;
                    return lockFile;
                }
            }
            catch (Exception ex)
            {
                log.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.Log_ErrorReadingLockFile,
                    path, ex.Message));

                // Ran into parsing errors, mark it as unlocked and out-of-date
                return new LockFile
                {
                    Version = int.MinValue,
                    Path = path
                };
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
                Write(textWriter, lockFile);
            }
        }

        public void Write(TextWriter textWriter, LockFile lockFile)
        {
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                var json = WriteLockFile(lockFile);
                json.WriteTo(jsonWriter);
            }
        }

        public string Render(LockFile lockFile)
        {
            using (var writer = new StringWriter())
            {
                Write(writer, lockFile);
                return writer.ToString();
            }
        }

        private static LockFile ReadLockFile(JObject cursor)
        {
            var lockFile = new LockFile();
            lockFile.Version = ReadInt(cursor, VersionProperty, defaultValue: int.MinValue);
            lockFile.Libraries = ReadObject(cursor[LibrariesProperty] as JObject, ReadLibrary);
            lockFile.Targets = ReadObject(cursor[TargetsProperty] as JObject, ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor[ProjectFileDependencyGroupsProperty] as JObject, ReadProjectFileDependencyGroup);
            lockFile.Tools = ReadObject(cursor[ToolsProperty] as JObject, ReadTarget);
            lockFile.ProjectFileToolGroups = ReadObject(cursor[ProjectFileToolGroupsProperty] as JObject, ReadProjectFileDependencyGroup);
            lockFile.PackageFolders = ReadObject(cursor[PackageFoldersProperty] as JObject, ReadFileItem);
            return lockFile;
        }

        private static JObject WriteLockFile(LockFile lockFile)
        {
            var json = new JObject();
            json[VersionProperty] = new JValue(lockFile.Version);
            json[TargetsProperty] = WriteObject(lockFile.Targets, WriteTarget);
            json[LibrariesProperty] = WriteObject(lockFile.Libraries, WriteLibrary);
            json[ProjectFileDependencyGroupsProperty] = WriteObject(lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup);

            // Avoid writing out the tools section for v1 lock files
            if (lockFile.Version >= 2)
            {
                if (lockFile.Tools != null)
                {
                    json[ToolsProperty] = WriteObject(lockFile.Tools, WriteTarget);
                }

                if (lockFile.ProjectFileToolGroups != null)
                {
                    json[ProjectFileToolGroupsProperty] = WriteObject(lockFile.ProjectFileToolGroups, WriteProjectFileDependencyGroup);
                }
            }

            if (lockFile.PackageFolders?.Any() == true)
            {
                json[PackageFoldersProperty] = WriteObject(lockFile.PackageFolders, WriteFileItem);
            }

            return json;
        }

        private static LockFileLibrary ReadLibrary(string property, JToken json)
        {
            var library = new LockFileLibrary();
            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = NuGetVersion.Parse(parts[1]);
            }

            library.Type = ReadString(json[TypeProperty]);

            var jObject = json as JObject;

            library.Path = ReadProperty<string>(jObject, PathProperty);
            library.MSBuildProject = ReadProperty<string>(jObject, MSBuildProjectProperty);
            library.Sha512 = ReadProperty<string>(jObject, Sha512Property);

            library.IsServiceable = ReadBool(json, ServicableProperty, defaultValue: false);
            library.Files = ReadPathArray(json[FilesProperty] as JArray, ReadString);

            return library;
        }

        private static JProperty WriteLibrary(LockFileLibrary library)
        {
            var json = new JObject();
            if (library.IsServiceable)
            {
                WriteBool(json, ServicableProperty, library.IsServiceable);
            }

            if (library.Sha512 != null)
            {
                json[Sha512Property] = WriteString(library.Sha512);
            }

            json[TypeProperty] = WriteString(library.Type);

            if (library.Path != null)
            {
                json[PathProperty] = WriteString(library.Path);
            }

            if (library.MSBuildProject != null)
            {
                json[MSBuildProjectProperty] = WriteString(library.MSBuildProject);
            }

            WritePathArray(json, FilesProperty, library.Files, WriteString);
            return new JProperty(
                library.Name + "/" + library.Version.ToNormalizedString(),
                json);
        }

        private static JProperty WriteTarget(LockFileTarget target)
        {
            var json = WriteObject(target.Libraries, WriteTargetLibrary);

            var key = target.Name;

            return new JProperty(key, json);
        }

        private static LockFileTarget ReadTarget(string property, JToken json)
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

        private static LockFileTargetLibrary ReadTargetLibrary(string property, JToken json)
        {
            var library = new LockFileTargetLibrary();

            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = NuGetVersion.Parse(parts[1]);
            }

            var jObject = json as JObject;
            library.Type = ReadProperty<string>(jObject, TypeProperty);
            library.Framework = ReadProperty<string>(jObject, FrameworkProperty);

            library.Dependencies = new HashSet<PackageDependency>(ReadObject(json[DependenciesProperty] as JObject, ReadPackageDependency));
            library.FrameworkAssemblies = ReadArray(json[FrameworkAssembliesProperty] as JArray, ReadString);
            library.RuntimeAssemblies = ReadObject(json[RuntimeProperty] as JObject, ReadFileItem);
            library.CompileTimeAssemblies = ReadObject(json[CompileProperty] as JObject, ReadFileItem);
            library.ResourceAssemblies = ReadObject(json[ResourceProperty] as JObject, ReadFileItem);
            library.NativeLibraries = ReadObject(json[NativeProperty] as JObject, ReadFileItem);
            library.ContentFiles = ReadObject(json[ContentFilesProperty] as JObject, ReadContentFile);
            library.RuntimeTargets = ReadObject(json[RuntimeTargetsProperty] as JObject, ReadRuntimeTarget);

            return library;
        }

        private static JProperty WriteTargetLibrary(LockFileTargetLibrary library)
        {
            var json = new JObject();

            if (library.Type != null)
            {
                json[TypeProperty] = library.Type;
            }

            if (library.Framework != null)
            {
                json[FrameworkProperty] = library.Framework;
            }

            if (library.Dependencies.Count > 0)
            {
                var ordered = library.Dependencies.OrderBy(dependency => dependency.Id, StringComparer.Ordinal);

                json[DependenciesProperty] = WriteObject(ordered, WritePackageDependency);
            }

            if (library.FrameworkAssemblies.Count > 0)
            {
                var ordered = library.FrameworkAssemblies.OrderBy(assembly => assembly, StringComparer.Ordinal);

                json[FrameworkAssembliesProperty] = WriteArray(ordered, WriteString);
            }

            if (library.CompileTimeAssemblies.Count > 0)
            {
                var ordered = library.CompileTimeAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[CompileProperty] = WriteObject(ordered, WriteFileItem);
            }

            if (library.RuntimeAssemblies.Count > 0)
            {
                var ordered = library.RuntimeAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[RuntimeProperty] = WriteObject(ordered, WriteFileItem);
            }

            if (library.ResourceAssemblies.Count > 0)
            {
                var ordered = library.ResourceAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[ResourceProperty] = WriteObject(ordered, WriteFileItem);
            }

            if (library.NativeLibraries.Count > 0)
            {
                var ordered = library.NativeLibraries.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[NativeProperty] = WriteObject(ordered, WriteFileItem);
            }

            if (library.ContentFiles.Count > 0)
            {
                var ordered = library.ContentFiles.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[ContentFilesProperty] = WriteObject(ordered, WriteFileItem);
            }

            if (library.RuntimeTargets.Count > 0)
            {
                var ordered = library.RuntimeTargets.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[RuntimeTargetsProperty] = WriteObject(ordered, WriteFileItem);
            }

            return new JProperty(library.Name + "/" + library.Version.ToNormalizedString(), json);
        }

        private static LockFileRuntimeTarget ReadRuntimeTarget(string property, JToken json)
        {
            return ReadFileItem(property, json, path => new LockFileRuntimeTarget(path));
        }

        private static LockFileContentFile ReadContentFile(string property, JToken json)
        {
            return ReadFileItem(property, json, path => new LockFileContentFile(path));
        }

        private static ProjectFileDependencyGroup ReadProjectFileDependencyGroup(string property, JToken json)
        {
            return new ProjectFileDependencyGroup(
                property,
                ReadArray(json as JArray, ReadString));
        }

        private static JProperty WriteProjectFileDependencyGroup(ProjectFileDependencyGroup frameworkInfo)
        {
            return new JProperty(
                frameworkInfo.FrameworkName,
                WriteArray(frameworkInfo.Dependencies, WriteString));
        }

        private static PackageDependencyGroup ReadPackageDependencyGroup(string property, JToken json)
        {
            var targetFramework = string.Equals(property, "*") ? null : new NuGetFramework(property);
            return new PackageDependencyGroup(
                targetFramework,
                new HashSet<PackageDependency>(ReadObject(json as JObject, ReadPackageDependency)));
        }

        private static JProperty WritePackageDependencyGroup(PackageDependencyGroup item)
        {
            return new JProperty(
                item.TargetFramework?.ToString() ?? "*",
                WriteObject(item.Packages, WritePackageDependency));
        }

        private static PackageDependency ReadPackageDependency(string property, JToken json)
        {
            var versionStr = json.Value<string>();
            return new PackageDependency(
                property,
                versionStr == null ? null : VersionRange.Parse(versionStr));
        }

        private static JProperty WritePackageDependency(PackageDependency item)
        {
            return new JProperty(
                item.Id,
                WriteString(item.VersionRange?.ToLegacyShortString()));
        }

        private static LockFileItem ReadFileItem(string property, JToken json)
        {
            return ReadFileItem(property, json, path => new LockFileItem(path));
        }

        private static T ReadFileItem<T>(string property, JToken json, Func<string, T> factory) where T: LockFileItem
        {
            var item = factory(property);
            foreach (var subProperty in json.OfType<JProperty>())
            {
                item.Properties[subProperty.Name] = subProperty.Value.Value<string>();
            }
            return item;
        }

        private static JProperty WriteFileItem(LockFileItem item)
        {
            return new JProperty(
                item.Path,
                new JObject(item.Properties.OrderBy(prop => prop.Key, StringComparer.Ordinal).Select(x =>
                {
                    if (Boolean.TrueString.Equals(x.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return new JProperty(x.Key, true);
                    }
                    else if (Boolean.FalseString.Equals(x.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return new JProperty(x.Key, false);
                    }
                    else
                    {
                        return new JProperty(x.Key, x.Value);
                    }
                })));
        }

        private static IList<TItem> ReadArray<TItem>(JArray json, Func<JToken, TItem> readItem)
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

        private static IList<string> ReadPathArray(JArray json, Func<JToken, string> readItem)
        {
            return ReadArray(json, readItem).Select(f => GetPathWithForwardSlashes(f)).ToList();
        }

        private static void WriteArray<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteArray(items, writeItem);
            }
        }

        private static void WritePathArray(JToken json, string property, IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            var orderedItems = items
                .Select(f => GetPathWithForwardSlashes(f))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            WriteArray(json, property, orderedItems, writeItem);
        }

        private static JArray WriteArray<TItem>(IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            var array = new JArray();
            foreach (var item in items)
            {
                array.Add(writeItem(item));
            }
            return array;
        }

        private static JArray WritePathArray(IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            return WriteArray(items.Select(f => GetPathWithForwardSlashes(f)), writeItem);
        }

        private static TItem ReadProperty<TItem>(JObject jObject, string propertyName)
        {
            if (jObject != null)
            {
                JToken value;
                if (jObject.TryGetValue(propertyName, out value) && value != null)
                {
                    return value.Value<TItem>();
                }
            }

            return default(TItem);
        }

        private static IList<TItem> ReadObject<TItem>(JObject jObject, Func<string, JToken, TItem> readItem)
        {
            if (jObject == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var child in jObject)
            {
                items.Add(readItem(child.Key, child.Value));
            }
            return items;
        }

        private static void WriteObject<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JProperty> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteObject(items, writeItem);
            }
        }

        private static JObject WriteObject<TItem>(IEnumerable<TItem> items, Func<TItem, JProperty> writeItem)
        {
            var array = new JObject();
            foreach (var item in items)
            {
                array.Add(writeItem(item));
            }
            return array;
        }

        private static bool ReadBool(JToken cursor, string property, bool defaultValue)
        {
            var valueToken = cursor[property];
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value<bool>();
        }

        private static int ReadInt(JToken cursor, string property, int defaultValue)
        {
            var valueToken = cursor[property];
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value<int>();
        }

        private static string ReadString(JToken json)
        {
            return json.Value<string>();
        }

        private static SemanticVersion ReadSemanticVersion(JToken json, string property)
        {
            var valueToken = json[property];
            if (valueToken == null)
            {
                throw new Exception(string.Format("TODO: lock file missing required property {0}", property));
            }
            return SemanticVersion.Parse(valueToken.Value<string>());
        }

        private static void WriteBool(JToken token, string property, bool value)
        {
            token[property] = new JValue(value);
        }

        private static JToken WriteString(string item)
        {
            return item != null ? new JValue(item) : JValue.CreateNull();
        }

        private static NuGetFramework ReadFrameworkName(JToken json)
        {
            return json == null ? null : new NuGetFramework(json.Value<string>());
        }

        private static JToken WriteFrameworkName(NuGetFramework item)
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
    }
}

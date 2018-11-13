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
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileFormat
    {
        public static readonly int Version = 3;
        public static readonly string LockFileName = "project.lock.json";
        public static readonly string AssetsFileName = "project.assets.json";

        private const string VersionProperty = "version";
        private const string LibrariesProperty = "libraries";
        private const string TargetsProperty = "targets";
        private const string ProjectFileDependencyGroupsProperty = "projectFileDependencyGroups";
        private const string ServicableProperty = "servicable";
        private const string Sha512Property = "sha512";
        private const string FilesProperty = "files";
        private const string HasToolsProperty = "hasTools";
        private const string DependenciesProperty = "dependencies";
        private const string FrameworkAssembliesProperty = "frameworkAssemblies";
        private const string RuntimeProperty = "runtime";
        private const string CompileProperty = "compile";
        private const string NativeProperty = "native";
        private const string BuildProperty = "build";
        private const string BuildMultiTargetingProperty = "buildMultiTargeting";
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
        private const string PackageSpecProperty = "project";
        private const string LogsProperty = "logs";
        private const string LinkProperty = "link";

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
                var json = JsonUtility.LoadJson(reader);
                var lockFile = ReadLockFile(json);
                lockFile.Path = path;
                return lockFile;
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
            // Create the directory if it does not exist
            var fileInfo = new FileInfo(filePath);
            fileInfo.Directory.Create();

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
            var lockFile = new LockFile()
            {
                Version = JsonUtility.ReadInt(cursor, VersionProperty, defaultValue: int.MinValue),
                Libraries = JsonUtility.ReadObject(cursor[LibrariesProperty] as JObject, ReadLibrary),
                Targets = JsonUtility.ReadObject(cursor[TargetsProperty] as JObject, ReadTarget),
                ProjectFileDependencyGroups = JsonUtility.ReadObject(cursor[ProjectFileDependencyGroupsProperty] as JObject, ReadProjectFileDependencyGroup),
                PackageFolders = JsonUtility.ReadObject(cursor[PackageFoldersProperty] as JObject, ReadFileItem),
                PackageSpec = ReadPackageSpec(cursor[PackageSpecProperty] as JObject)
            };

            lockFile.LogMessages = ReadLogMessageArray(cursor[LogsProperty] as JArray,
                lockFile?.PackageSpec?.RestoreMetadata?.ProjectPath);

            return lockFile;
        }

        private static JObject WriteLockFile(LockFile lockFile)
        {
            var json = new JObject
            {
                [VersionProperty] = new JValue(lockFile.Version),
                [TargetsProperty] = JsonUtility.WriteObject(lockFile.Targets, WriteTarget),
                [LibrariesProperty] = JsonUtility.WriteObject(lockFile.Libraries, WriteLibrary),
                [ProjectFileDependencyGroupsProperty] = JsonUtility.WriteObject(lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup)
            };
            if (lockFile.PackageFolders?.Any() == true)
            {
                json[PackageFoldersProperty] = JsonUtility.WriteObject(lockFile.PackageFolders, WriteFileItem);
            }

            if (lockFile.Version >= 2)
            {
                if (lockFile.PackageSpec != null)
                {
                    var writer = new JsonObjectWriter();
                    PackageSpecWriter.Write(lockFile.PackageSpec, writer);
                    var packageSpec = writer.GetJObject();
                    json[PackageSpecProperty] = packageSpec;
                }
            }

            if(lockFile.Version >= 3)
            {
                if(lockFile.LogMessages.Count > 0)
                {
                    var projectPath = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath;
                    json[LogsProperty] = WriteLogMessages(lockFile.LogMessages, projectPath);
                }
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

            library.Path = JsonUtility.ReadProperty<string>(jObject, PathProperty);
            library.MSBuildProject = JsonUtility.ReadProperty<string>(jObject, MSBuildProjectProperty);
            library.Sha512 = JsonUtility.ReadProperty<string>(jObject, Sha512Property);

            library.IsServiceable = ReadBool(json, ServicableProperty, defaultValue: false);
            library.Files = ReadPathArray(json[FilesProperty] as JArray, ReadString);

            library.HasTools = ReadBool(json, HasToolsProperty, defaultValue: false);
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
                json[Sha512Property] = JsonUtility.WriteString(library.Sha512);
            }

            json[TypeProperty] = JsonUtility.WriteString(library.Type);

            if (library.Path != null)
            {
                json[PathProperty] = JsonUtility.WriteString(library.Path);
            }

            if (library.MSBuildProject != null)
            {
                json[MSBuildProjectProperty] = JsonUtility.WriteString(library.MSBuildProject);
            }

            if (library.HasTools)
            {
                WriteBool(json, HasToolsProperty, library.HasTools);
            }

            WritePathArray(json, FilesProperty, library.Files, JsonUtility.WriteString);
            
            return new JProperty(
                library.Name + "/" + library.Version.ToNormalizedString(),
                json);
        }

        private static JProperty WriteTarget(LockFileTarget target)
        {
            var json = JsonUtility.WriteObject(target.Libraries, WriteTargetLibrary);

            var key = target.Name;

            return new JProperty(key, json);
        }

        private static LockFileTarget ReadTarget(string property, JToken json)
        {
            var target = new LockFileTarget();
            var parts = property.Split(JsonUtility.PathSplitChars, 2);
            target.TargetFramework = NuGetFramework.Parse(parts[0]);
            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = parts[1];
            }

            target.Libraries = JsonUtility.ReadObject(json as JObject, ReadTargetLibrary);

            return target;
        }

        /// <summary>
        /// Converts the <code>IAssetsLogMessage</code> object into a <code>JObject</code> that can be written into the assets file.
        /// </summary>
        /// <param name="logMessage"><code>IAssetsLogMessage</code> representing the log message.</param>
        /// <returns><code>JObject</code> containg the json representation of the log message.</returns>
        private static JObject WriteLogMessage(IAssetsLogMessage logMessage, string projectPath)
        {
            var logJObject = new JObject()
            {
                [LogMessageProperties.CODE] = Enum.GetName(typeof(NuGetLogCode), logMessage.Code),
                [LogMessageProperties.LEVEL] = Enum.GetName(typeof(LogLevel), logMessage.Level)
            };

            if (logMessage.Level == LogLevel.Warning)
            {
                logJObject[LogMessageProperties.WARNING_LEVEL] = (int)logMessage.WarningLevel;
            }

            if (logMessage.FilePath != null && 
               (projectPath == null || !PathUtility.GetStringComparerBasedOnOS().Equals(logMessage.FilePath, projectPath)))
            {
                // Do not write the file path if it is the same as the project path.
                // This prevents duplicate information in the lock file.
                logJObject[LogMessageProperties.FILE_PATH] = logMessage.FilePath;
            }

            if (logMessage.StartLineNumber > 0)
            {
                logJObject[LogMessageProperties.START_LINE_NUMBER] = logMessage.StartLineNumber;
            }

            if (logMessage.StartColumnNumber > 0)
            {
                logJObject[LogMessageProperties.START_COLUMN_NUMBER] = logMessage.StartColumnNumber;
            }

            if (logMessage.EndLineNumber > 0)
            {
                logJObject[LogMessageProperties.END_LINE_NUMBER] = logMessage.EndLineNumber;
            }

            if (logMessage.EndColumnNumber > 0)
            {
                logJObject[LogMessageProperties.END_COLUMN_NUMBER] = logMessage.EndColumnNumber;
            }

            if (logMessage.Message != null)
            {
                logJObject[LogMessageProperties.MESSAGE] = logMessage.Message;
            }

            if (logMessage.LibraryId != null)
            {
                logJObject[LogMessageProperties.LIBRARY_ID] = logMessage.LibraryId;
            }

            if (logMessage.TargetGraphs != null && 
                logMessage.TargetGraphs.Any() && 
                logMessage.TargetGraphs.All(l => !string.IsNullOrEmpty(l)))
            {
                logJObject[LogMessageProperties.TARGET_GRAPHS] = new JArray(logMessage.TargetGraphs);
            }

            return logJObject;
        }

        /// <summary>
        /// Converts an <code>JObject</code> into an <code>IAssetsLogMessage</code>.
        /// </summary>
        /// <param name="json"><code>JObject</code> containg the json representation of the log message.</param>
        /// <returns><code>IAssetsLogMessage</code> representing the log message.</returns>
        private static IAssetsLogMessage ReadLogMessage(JObject json, string projectPath)
        {
            AssetsLogMessage assetsLogMessage = null;

            if (json != null)
            {

                var levelJson = json[LogMessageProperties.LEVEL];
                var codeJson = json[LogMessageProperties.CODE];
                var warningLevelJson = json[LogMessageProperties.WARNING_LEVEL];
                var filePathJson = json[LogMessageProperties.FILE_PATH];
                var startLineNumberJson = json[LogMessageProperties.START_LINE_NUMBER];
                var startColumnNumberJson = json[LogMessageProperties.START_COLUMN_NUMBER];
                var endLineNumberJson = json[LogMessageProperties.END_LINE_NUMBER];
                var endColumnNumberJson = json[LogMessageProperties.END_COLUMN_NUMBER];
                var messageJson = json[LogMessageProperties.MESSAGE];
                var libraryIdJson = json[LogMessageProperties.LIBRARY_ID];

                var isValid = true;

                isValid &= Enum.TryParse(levelJson.Value<string>(), out LogLevel level);
                isValid &= Enum.TryParse(codeJson.Value<string>(), out NuGetLogCode code);

                if (isValid)
                {
                    assetsLogMessage = new AssetsLogMessage(level, code, messageJson.Value<string>())
                    {
                        TargetGraphs = (IReadOnlyList<string>)ReadArray(json[LogMessageProperties.TARGET_GRAPHS] as JArray, ReadString)
                    };

                    if (level == LogLevel.Warning && warningLevelJson != null)
                    {
                        assetsLogMessage.WarningLevel = (WarningLevel)Enum.ToObject(typeof(WarningLevel), warningLevelJson.Value<int>());
                    }

                    if (filePathJson != null)
                    {
                        assetsLogMessage.FilePath = filePathJson.Value<string>();
                    }
                    else
                    {
                        assetsLogMessage.FilePath = projectPath;
                    }

                    if (startLineNumberJson != null)
                    {
                        assetsLogMessage.StartLineNumber = startLineNumberJson.Value<int>();
                    }

                    if (startColumnNumberJson != null)
                    {
                        assetsLogMessage.StartColumnNumber = startColumnNumberJson.Value<int>();
                    }

                    if (endLineNumberJson != null)
                    {
                        assetsLogMessage.EndLineNumber = endLineNumberJson.Value<int>();
                    }

                    if (endColumnNumberJson != null)
                    {
                        assetsLogMessage.EndColumnNumber = endColumnNumberJson.Value<int>();
                    }

                    if (libraryIdJson != null)
                    {
                        assetsLogMessage.LibraryId = libraryIdJson.Value<string>();
                    }
                }
            }

            return assetsLogMessage;
        }

        private static JArray WriteLogMessages(IEnumerable<IAssetsLogMessage> logMessages, string projectPath)
        {
            var logMessageArray = new JArray();
            foreach(var logMessage in logMessages)
            {
                logMessageArray.Add(WriteLogMessage(logMessage, projectPath));
            }
            return logMessageArray;
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
            library.Type = JsonUtility.ReadProperty<string>(jObject, TypeProperty);
            library.Framework = JsonUtility.ReadProperty<string>(jObject, FrameworkProperty);

            library.Dependencies = JsonUtility.ReadObject(json[DependenciesProperty] as JObject, JsonUtility.ReadPackageDependency);
            library.FrameworkAssemblies = ReadArray(json[FrameworkAssembliesProperty] as JArray, ReadString);
            library.RuntimeAssemblies = JsonUtility.ReadObject(json[RuntimeProperty] as JObject, ReadFileItem);
            library.CompileTimeAssemblies = JsonUtility.ReadObject(json[CompileProperty] as JObject, ReadFileItem);
            library.ResourceAssemblies = JsonUtility.ReadObject(json[ResourceProperty] as JObject, ReadFileItem);
            library.NativeLibraries = JsonUtility.ReadObject(json[NativeProperty] as JObject, ReadFileItem);
            library.Build = JsonUtility.ReadObject(json[BuildProperty] as JObject, ReadFileItem);
            library.BuildMultiTargeting = JsonUtility.ReadObject(json[BuildMultiTargetingProperty] as JObject, ReadFileItem);
            library.ContentFiles = JsonUtility.ReadObject(json[ContentFilesProperty] as JObject, ReadContentFile);
            library.RuntimeTargets = JsonUtility.ReadObject(json[RuntimeTargetsProperty] as JObject, ReadRuntimeTarget);
            library.ToolsAssemblies = JsonUtility.ReadObject(json[ToolsProperty] as JObject, ReadFileItem);
            library.LinkAssemblies = JsonUtility.ReadObject(json[LinkProperty] as JObject, ReadFileItem);

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

                json[DependenciesProperty] = JsonUtility.WriteObject(ordered, JsonUtility.WritePackageDependency);
            }

            if (library.FrameworkAssemblies.Count > 0)
            {
                var ordered = library.FrameworkAssemblies.OrderBy(assembly => assembly, StringComparer.Ordinal);

                json[FrameworkAssembliesProperty] = WriteArray(ordered, JsonUtility.WriteString);
            }

            if (library.CompileTimeAssemblies.Count > 0)
            {
                var ordered = library.CompileTimeAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[CompileProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.RuntimeAssemblies.Count > 0)
            {
                var ordered = library.RuntimeAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[RuntimeProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.ResourceAssemblies.Count > 0)
            {
                var ordered = library.ResourceAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[ResourceProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.NativeLibraries.Count > 0)
            {
                var ordered = library.NativeLibraries.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[NativeProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.ContentFiles.Count > 0)
            {
                var ordered = library.ContentFiles.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[ContentFilesProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.Build.Count > 0)
            {
                var ordered = library.Build.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[BuildProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.BuildMultiTargeting.Count > 0)
            {
                var ordered = library.BuildMultiTargeting.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[BuildMultiTargetingProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.RuntimeTargets.Count > 0)
            {
                var ordered = library.RuntimeTargets.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[RuntimeTargetsProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.ToolsAssemblies.Count > 0)
            {
                var ordered = library.ToolsAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                json[ToolsProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
            }

            if (library.LinkAssemblies.Count > 0)
            {
                var ordered = library.LinkAssemblies.OrderBy(assemby => assemby.Path, StringComparer.Ordinal);

                json[LinkProperty] = JsonUtility.WriteObject(ordered, WriteFileItem);
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

        private static PackageSpec ReadPackageSpec(JObject json)
        {
            if (json == null)
            {
                return null;
            }

            return JsonPackageSpecReader.GetPackageSpec(
                json,
                name: null,
                packageSpecPath: null,
                snapshotValue: null);
        }

        private static JProperty WriteProjectFileDependencyGroup(ProjectFileDependencyGroup frameworkInfo)
        {
            return new JProperty(
                frameworkInfo.FrameworkName,
                WriteArray(frameworkInfo.Dependencies, JsonUtility.WriteString));
        }

        private static PackageDependencyGroup ReadPackageDependencyGroup(string property, JToken json)
        {
            var targetFramework = string.Equals(property, "*") ? null : new NuGetFramework(property);
            return new PackageDependencyGroup(
                targetFramework,
                JsonUtility.ReadObject(json as JObject, JsonUtility.ReadPackageDependency));
        }

        private static JProperty WritePackageDependencyGroup(PackageDependencyGroup item)
        {
            return new JProperty(
                item.TargetFramework?.ToString() ?? "*",
                JsonUtility.WriteObject(item.Packages, JsonUtility.WritePackageDependency));
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
                    if (bool.TrueString.Equals(x.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return new JProperty(x.Key, true);
                    }
                    else if (bool.FalseString.Equals(x.Value, StringComparison.OrdinalIgnoreCase))
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
                var item = readItem(child);
                if(item != null)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        private static IList<IAssetsLogMessage> ReadLogMessageArray(JArray json, string projectPath)
        {
            if (json == null)
            {
                return new List<IAssetsLogMessage>();
            }

            var items = new List<IAssetsLogMessage>();
            foreach (var child in json)
            {
                var logMessage = ReadLogMessage(child as JObject, projectPath);
                if (logMessage != null)
                {
                    items.Add(logMessage);
                }
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

        private static void WriteObject<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JProperty> writeItem)
        {
            if (items.Any())
            {
                json[property] = JsonUtility.WriteObject(items, writeItem);
            }
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

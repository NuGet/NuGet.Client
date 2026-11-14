// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Reads and writes the <c>project.assets.json</c> file used by restore consumers.
    /// </summary>
    public class LockFileFormat
    {
        /// <summary>
        /// The current assets file format version.
        /// </summary>
        public static readonly int Version = 4;

        /// <summary>
        /// The assets file version that uses target aliases instead of framework names for multi-targeted projects.
        /// </summary>
        public const int AliasedVersion = 4;

        /// <summary>
        /// The assets file version used by classic projects and SDK versions earlier than 10.0.300.
        /// </summary>
        public const int LegacyVersion = 3;

        // If this is ever renamed, you should also rename NoOpRestoreUtilities.NoOpCacheFileName to keep them in sync.
        /// <summary>
        /// The standard assets file name.
        /// </summary>
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
        private const string AnalyzersProperty = "analyzers";
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
        private const string PackageFoldersProperty = "packageFolders";
        private const string PackageSpecProperty = "project";
        internal const string LogsProperty = "logs";
        private const string EmbedProperty = "embed";
        private const string FrameworkReferencesProperty = "frameworkReferences";
        private const string CentralTransitiveDependencyGroupsProperty = "centralTransitiveDependencyGroups";

        private const int StackallocCharThreshold = 256;
        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Indented = true,
            // The explicit writer controls the complete JSON structure. String escaping remains enabled.
            SkipValidation = true
        };

        /// <summary>
        /// Caches fixed schema property names in their encoded form to avoid transcoding them for every write,
        /// which is a measurable cost on .NET Framework.
        /// </summary>
        private static class EncodedPropertyNames
        {
            public static readonly JsonEncodedText Version = JsonEncodedText.Encode(VersionProperty);
            public static readonly JsonEncodedText Libraries = JsonEncodedText.Encode(LibrariesProperty);
            public static readonly JsonEncodedText Targets = JsonEncodedText.Encode(TargetsProperty);
            public static readonly JsonEncodedText ProjectFileDependencyGroups = JsonEncodedText.Encode(ProjectFileDependencyGroupsProperty);
            public static readonly JsonEncodedText Servicable = JsonEncodedText.Encode(ServicableProperty);
            public static readonly JsonEncodedText Sha512 = JsonEncodedText.Encode(Sha512Property);
            public static readonly JsonEncodedText Files = JsonEncodedText.Encode(FilesProperty);
            public static readonly JsonEncodedText HasTools = JsonEncodedText.Encode(HasToolsProperty);
            public static readonly JsonEncodedText Dependencies = JsonEncodedText.Encode(DependenciesProperty);
            public static readonly JsonEncodedText FrameworkAssemblies = JsonEncodedText.Encode(FrameworkAssembliesProperty);
            public static readonly JsonEncodedText Runtime = JsonEncodedText.Encode(RuntimeProperty);
            public static readonly JsonEncodedText Compile = JsonEncodedText.Encode(CompileProperty);
            public static readonly JsonEncodedText Analyzers = JsonEncodedText.Encode(AnalyzersProperty);
            public static readonly JsonEncodedText Native = JsonEncodedText.Encode(NativeProperty);
            public static readonly JsonEncodedText Build = JsonEncodedText.Encode(BuildProperty);
            public static readonly JsonEncodedText BuildMultiTargeting = JsonEncodedText.Encode(BuildMultiTargetingProperty);
            public static readonly JsonEncodedText ContentFiles = JsonEncodedText.Encode(ContentFilesProperty);
            public static readonly JsonEncodedText RuntimeTargets = JsonEncodedText.Encode(RuntimeTargetsProperty);
            public static readonly JsonEncodedText Resource = JsonEncodedText.Encode(ResourceProperty);
            public static readonly JsonEncodedText Type = JsonEncodedText.Encode(TypeProperty);
            public static readonly JsonEncodedText Path = JsonEncodedText.Encode(PathProperty);
            public static readonly JsonEncodedText MSBuildProject = JsonEncodedText.Encode(MSBuildProjectProperty);
            public static readonly JsonEncodedText Framework = JsonEncodedText.Encode(FrameworkProperty);
            public static readonly JsonEncodedText Tools = JsonEncodedText.Encode(ToolsProperty);
            public static readonly JsonEncodedText PackageFolders = JsonEncodedText.Encode(PackageFoldersProperty);
            public static readonly JsonEncodedText PackageSpec = JsonEncodedText.Encode(PackageSpecProperty);
            public static readonly JsonEncodedText Logs = JsonEncodedText.Encode(LogsProperty);
            public static readonly JsonEncodedText Embed = JsonEncodedText.Encode(EmbedProperty);
            public static readonly JsonEncodedText FrameworkReferences = JsonEncodedText.Encode(FrameworkReferencesProperty);
            public static readonly JsonEncodedText CentralTransitiveDependencyGroups = JsonEncodedText.Encode(CentralTransitiveDependencyGroupsProperty);
            public static readonly JsonEncodedText LogCode = JsonEncodedText.Encode("code");
            public static readonly JsonEncodedText LogLevel = JsonEncodedText.Encode("level");
            public static readonly JsonEncodedText LogWarningLevel = JsonEncodedText.Encode("warningLevel");
            public static readonly JsonEncodedText LogFilePath = JsonEncodedText.Encode("filePath");
            public static readonly JsonEncodedText LogStartLineNumber = JsonEncodedText.Encode("startLineNumber");
            public static readonly JsonEncodedText LogStartColumnNumber = JsonEncodedText.Encode("startColumnNumber");
            public static readonly JsonEncodedText LogEndLineNumber = JsonEncodedText.Encode("endLineNumber");
            public static readonly JsonEncodedText LogEndColumnNumber = JsonEncodedText.Encode("endColumnNumber");
            public static readonly JsonEncodedText LogMessage = JsonEncodedText.Encode("message");
            public static readonly JsonEncodedText LogLibraryId = JsonEncodedText.Encode("libraryId");
            public static readonly JsonEncodedText LogTargetGraphs = JsonEncodedText.Encode("targetGraphs");
        }

        /// <summary>
        /// Parses complete assets file content and uses <paramref name="path"/> in diagnostics.
        /// </summary>
        /// <remarks>
        /// This method allocates a UTF-8 copy of the complete string. Use a stream overload to avoid the copy.
        /// </remarks>
        public LockFile Parse(string lockFileContent, string path)
        {
            return Parse(lockFileContent, NullLogger.Instance, path);
        }

        /// <summary>
        /// Parses complete assets file content and logs parsing failures.
        /// </summary>
        /// <remarks>
        /// This method allocates a UTF-8 copy of the complete string. Use a stream overload to avoid the copy.
        /// </remarks>
        public LockFile Parse(string lockFileContent, ILogger log, string path)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(lockFileContent);
            using (var stream = new MemoryStream(byteArray))
            {
                return Read(stream, log, path);
            }
        }

        /// <summary>
        /// Reads an assets file and returns an invalid lock file when parsing fails.
        /// </summary>
        public LockFile Read(string filePath)
        {
            return Read(filePath, NullLogger.Instance);
        }

        /// <summary>
        /// Reads an assets file and logs parsing failures.
        /// </summary>
        public LockFile Read(string filePath, ILogger log)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Read(stream, log, filePath);
            }
        }

        /// <summary>
        /// Reads UTF-8 assets data without disposing the source stream.
        /// </summary>
        /// <remarks>
        /// The caller retains ownership of <paramref name="stream"/>. Parsing starts at the current stream position.
        /// </remarks>
        public LockFile Read(Stream stream, string path)
        {
            return Read(stream, NullLogger.Instance, path);
        }

#pragma warning disable CA1822 // Mark members as static - public API
        /// <summary>
        /// Reads UTF-8 assets data, logs failures, and does not dispose the source stream.
        /// </summary>
        /// <remarks>
        /// The caller retains ownership of <paramref name="stream"/>. Parsing starts at the current stream position.
        /// </remarks>
        public LockFile Read(Stream stream, ILogger log, string path)
#pragma warning restore CA1822 // Mark members as static
        {
            return Read(stream, log, path, flags: LockFileReadFlags.All);
        }

        internal static LockFile Read(Stream stream, ILogger log, string path, LockFileReadFlags flags)
        {
            return Utf8JsonRead(stream, log, path, flags);
        }

        /// <summary>
        /// Creates the parent directory and replaces an existing assets file.
        /// </summary>
        /// <param name="filePath">The destination assets file path.</param>
        /// <param name="lockFile">The assets file model to write.</param>
        /// <remarks>
        /// This method writes directly to the destination file. A writing failure can leave partial output.
        /// </remarks>
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

#pragma warning disable CA1822 // Mark members as static - public API
        /// <summary>
        /// Writes UTF-8 assets data and disposes the destination stream before returning.
        /// </summary>
        /// <param name="stream">The destination stream. This method takes ownership of the stream.</param>
        /// <param name="lockFile">The assets file model to write.</param>
        /// <remarks>
        /// This method writes incrementally through a pooled buffer. A writing failure can leave partial output.
        /// </remarks>
        public void Write(Stream stream, LockFile lockFile)
#pragma warning restore CA1822 // Mark members as static
        {
            using (stream)
            {
                WriteToStream(stream, lockFile);
            }
        }

        /// <summary>
        /// Uses the legacy Newtonsoft.Json writer and disposes the destination text writer.
        /// </summary>
        /// <param name="textWriter">The destination text writer. This method takes ownership of the writer.</param>
        /// <param name="lockFile">The assets file model to write.</param>
        /// <remarks>
        /// A writing failure can leave partial output. Use a stream overload for native System.Text.Json writing
        /// and NativeAOT compatibility.
        /// </remarks>
        [Obsolete("Use Write(Stream, LockFile) instead for native System.Text.Json writing and NativeAOT compatibility.")]
        public void Write(TextWriter textWriter, LockFile lockFile)
        {
            using (var jsonWriter = new JsonTextWriter(textWriter))
            using (var jsonObjectWriter = new JsonObjectWriter(jsonWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                WriteLockFile(jsonWriter, jsonObjectWriter, lockFile);
            }
        }

#pragma warning disable CA1822 // Mark members as static
        /// <summary>
        /// Renders the complete assets file as a string.
        /// </summary>
        /// <remarks>
        /// This method buffers the complete UTF-8 output before it creates the returned UTF-16 string.
        /// </remarks>
        /// <param name="lockFile">The assets file model to render.</param>
        /// <returns>The rendered assets file.</returns>
        [Obsolete("Use Write(Stream, LockFile) instead to avoid buffering the entire lock file in memory.")]
        public string Render(LockFile lockFile)
#pragma warning restore CA1822 // Mark members as static
        {
            using (var stream = new MemoryStream())
            {
                WriteToStream(stream, lockFile);
                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
            }
        }

        private static LockFile Utf8JsonRead(Stream stream, ILogger log, string path, LockFileReadFlags flags)
        {
            try
            {
                var lockFile = JsonUtility.LoadJson(stream, Utf8JsonStreamLockFileConverters.LockFileConverter, flags);
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

        private static void WriteToStream(Stream stream, LockFile lockFile)
        {
            using (var bufferWriter = new PooledStreamBufferWriter(stream))
            using (var writer = new Utf8JsonWriter(bufferWriter, WriterOptions))
            {
                WriteLockFile(writer, lockFile);
                writer.Flush();
                bufferWriter.Flush();
            }
        }

        private static void WriteLockFile(Utf8JsonWriter writer, LockFile lockFile)
        {
            var objectWriter = new Utf8JsonObjectWriter(writer);

            writer.WriteStartObject();
            writer.WriteNumber(EncodedPropertyNames.Version, lockFile.Version);

            writer.WritePropertyName(EncodedPropertyNames.Targets);
            WriteObject(writer, lockFile.Targets, WriteTarget);

            writer.WritePropertyName(EncodedPropertyNames.Libraries);
            WriteObject(writer, lockFile.Libraries, WriteLibrary);

            writer.WritePropertyName(EncodedPropertyNames.ProjectFileDependencyGroups);
            WriteObject(writer, lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup);

            if (lockFile.PackageFolders?.Any() == true)
            {
                writer.WritePropertyName(EncodedPropertyNames.PackageFolders);
                WriteObject(writer, lockFile.PackageFolders, WriteFileItem);
            }

            if (lockFile.Version >= 2 && lockFile.PackageSpec != null)
            {
                writer.WritePropertyName(EncodedPropertyNames.PackageSpec);
                writer.WriteStartObject();

                PackageSpecWriter.Write(
                    lockFile.PackageSpec,
                    objectWriter,
                    hashing: false,
                    EnvironmentVariableWrapper.Instance,
                    useLegacyWriter: lockFile.Version <= LegacyVersion);

                writer.WriteEndObject();
            }

            if (lockFile.Version >= 3 && lockFile.LogMessages.Count > 0)
            {
                string projectPath = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath;
                writer.WritePropertyName(EncodedPropertyNames.Logs);
                WriteLogMessages(writer, lockFile.LogMessages, projectPath);
            }

            if (lockFile.CentralTransitiveDependencyGroups.Any())
            {
                writer.WritePropertyName(EncodedPropertyNames.CentralTransitiveDependencyGroups);
                WriteCentralTransitiveDependencyGroup(objectWriter, lockFile.CentralTransitiveDependencyGroups);
            }

            writer.WriteEndObject();
        }

        private static void WriteLibrary(Utf8JsonWriter writer, LockFileLibrary library)
        {
            WriteLibraryPropertyName(writer, library.Name, library.Version);
            writer.WriteStartObject();

            if (library.IsServiceable)
            {
                writer.WriteBoolean(EncodedPropertyNames.Servicable, library.IsServiceable);
            }

            if (library.Sha512 != null)
            {
                writer.WriteString(EncodedPropertyNames.Sha512, library.Sha512);
            }

            writer.WriteString(EncodedPropertyNames.Type, library.Type);

            if (library.Path != null)
            {
                writer.WriteString(EncodedPropertyNames.Path, library.Path);
            }

            if (library.MSBuildProject != null)
            {
                writer.WriteString(EncodedPropertyNames.MSBuildProject, library.MSBuildProject);
            }

            if (library.HasTools)
            {
                writer.WriteBoolean(EncodedPropertyNames.HasTools, library.HasTools);
            }

            WritePathArray(writer, EncodedPropertyNames.Files, library.Files);
            writer.WriteEndObject();
        }

        private static void WriteTarget(Utf8JsonWriter writer, LockFileTarget target)
        {
            writer.WritePropertyName(target.Name);
            WriteObject(writer, target.Libraries, WriteTargetLibrary);
        }

        private static void WriteLogMessage(Utf8JsonWriter writer, IAssetsLogMessage logMessage, string projectPath)
        {
            writer.WriteStartObject();
            writer.WriteString(EncodedPropertyNames.LogCode, GetEnumName(logMessage.Code));
            writer.WriteString(EncodedPropertyNames.LogLevel, GetEnumName(logMessage.Level));

            if (logMessage.Level == LogLevel.Warning)
            {
                writer.WriteNumber(EncodedPropertyNames.LogWarningLevel, (int)logMessage.WarningLevel);
            }

            if (logMessage.FilePath != null &&
                (projectPath == null || !PathUtility.GetStringComparerBasedOnOS().Equals(logMessage.FilePath, projectPath)))
            {
                writer.WriteString(EncodedPropertyNames.LogFilePath, logMessage.FilePath);
            }

            if (logMessage.StartLineNumber > 0)
            {
                writer.WriteNumber(EncodedPropertyNames.LogStartLineNumber, logMessage.StartLineNumber);
            }

            if (logMessage.StartColumnNumber > 0)
            {
                writer.WriteNumber(EncodedPropertyNames.LogStartColumnNumber, logMessage.StartColumnNumber);
            }

            if (logMessage.EndLineNumber > 0)
            {
                writer.WriteNumber(EncodedPropertyNames.LogEndLineNumber, logMessage.EndLineNumber);
            }

            if (logMessage.EndColumnNumber > 0)
            {
                writer.WriteNumber(EncodedPropertyNames.LogEndColumnNumber, logMessage.EndColumnNumber);
            }

            if (logMessage.Message != null)
            {
                writer.WriteString(EncodedPropertyNames.LogMessage, logMessage.Message);
            }

            if (logMessage.LibraryId != null)
            {
                writer.WriteString(EncodedPropertyNames.LogLibraryId, logMessage.LibraryId);
            }

            if (logMessage.TargetGraphs != null &&
                logMessage.TargetGraphs.Any() &&
                logMessage.TargetGraphs.All(targetGraph => !string.IsNullOrEmpty(targetGraph)))
            {
                writer.WritePropertyName(EncodedPropertyNames.LogTargetGraphs);
                WriteArray(writer, logMessage.TargetGraphs);
            }

            writer.WriteEndObject();
        }

        private static string GetEnumName<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
#if NET5_0_OR_GREATER
            return Enum.GetName(value);
#else
            return Enum.GetName(typeof(TEnum), value);
#endif
        }

        private static void WriteLogMessages(Utf8JsonWriter writer, IEnumerable<IAssetsLogMessage> logMessages, string projectPath)
        {
            writer.WriteStartArray();

            foreach (IAssetsLogMessage logMessage in logMessages)
            {
                WriteLogMessage(writer, logMessage, projectPath);
            }

            writer.WriteEndArray();
        }

        private static void WriteTargetLibrary(Utf8JsonWriter writer, LockFileTargetLibrary library)
        {
            WriteLibraryPropertyName(writer, library.Name, library.Version);
            writer.WriteStartObject();

            if (library.Type != null)
            {
                writer.WriteString(EncodedPropertyNames.Type, library.Type);
            }

            if (library.Framework != null)
            {
                writer.WriteString(EncodedPropertyNames.Framework, library.Framework);
            }

            if (library.Dependencies.Count > 0)
            {
                writer.WritePropertyName(EncodedPropertyNames.Dependencies);
                writer.WriteStartObject();

                WriteOrdinallySorted(
                    writer,
                    library.Dependencies,
                    static dependency => dependency.Id,
                    WritePackageDependencyWithLegacyString);

                writer.WriteEndObject();
            }

            WriteSortedArray(writer, EncodedPropertyNames.FrameworkAssemblies, library.FrameworkAssemblies);

            WriteItems(writer, EncodedPropertyNames.Compile, library.CompileTimeAssemblies);
            WriteItems(writer, EncodedPropertyNames.Analyzers, library.AnalyzerAssets);
            WriteItems(writer, EncodedPropertyNames.Runtime, library.RuntimeAssemblies);

            WriteSortedArray(writer, EncodedPropertyNames.FrameworkReferences, library.FrameworkReferences);

            WriteItems(writer, EncodedPropertyNames.Resource, library.ResourceAssemblies);
            WriteItems(writer, EncodedPropertyNames.Native, library.NativeLibraries);
            WriteItems(writer, EncodedPropertyNames.ContentFiles, library.ContentFiles);
            WriteItems(writer, EncodedPropertyNames.Build, library.Build);
            WriteItems(writer, EncodedPropertyNames.BuildMultiTargeting, library.BuildMultiTargeting);
            WriteItems(writer, EncodedPropertyNames.RuntimeTargets, library.RuntimeTargets);
            WriteItems(writer, EncodedPropertyNames.Tools, library.ToolsAssemblies);
            WriteItems(writer, EncodedPropertyNames.Embed, library.EmbedAssemblies);

            writer.WriteEndObject();
        }

        private static void WriteItems<T>(Utf8JsonWriter writer, JsonEncodedText propertyName, IList<T> items)
            where T : LockFileItem
        {
            if (items.Count > 0)
            {
                writer.WritePropertyName(propertyName);
                writer.WriteStartObject();

                WriteOrdinallySorted(
                    writer,
                    items,
                    static item => item.Path,
                    static (jsonWriter, item) => WriteFileItem(jsonWriter, item));

                writer.WriteEndObject();
            }
        }

        private static void WriteProjectFileDependencyGroup(
            Utf8JsonWriter writer,
            ProjectFileDependencyGroup frameworkInfo)
        {
            writer.WritePropertyName(frameworkInfo.FrameworkName);
            WriteArray(writer, frameworkInfo.Dependencies);
        }

        private static void WriteFileItem(Utf8JsonWriter writer, LockFileItem item)
        {
            writer.WritePropertyName(item.Path);
            writer.WriteStartObject();

            WriteOrdinallySorted(
                writer,
                item.Properties,
                static property => property.Key,
                WriteFileItemProperty);

            writer.WriteEndObject();
        }

        private static void WriteFileItemProperty(
            Utf8JsonWriter writer,
            KeyValuePair<string, string> property)
        {
            if (bool.TrueString.Equals(property.Value, StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteBoolean(property.Key, true);
            }
            else if (bool.FalseString.Equals(property.Value, StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteBoolean(property.Key, false);
            }
            else
            {
                writer.WriteString(property.Key, property.Value);
            }
        }

        private static void WritePathArray(Utf8JsonWriter writer, JsonEncodedText property, IEnumerable<string> items)
        {
            using var itemsEnumerator = items.NoAllocEnumerate().GetEnumerator();
            if (itemsEnumerator.MoveNext())
            {
                IEnumerable<string> orderedItems = items
                    .Select(GetPathWithForwardSlashes)
                    .OrderBy(item => item, StringComparer.Ordinal);

                writer.WritePropertyName(property);
                WriteArray(writer, orderedItems);
            }
        }

        private static void WritePackageDependencyWithLegacyString(
            Utf8JsonWriter writer,
            PackageDependency dependency)
        {
            writer.WritePropertyName(dependency.Id);
            writer.WriteStringValue(dependency.VersionRange?.ToNonSnapshotRange().ToLegacyShortString());
        }

        private static void WriteLibraryPropertyName(Utf8JsonWriter writer, string name, NuGetVersion version)
        {
            string normalizedVersion = version.ToNormalizedString();
            int propertyNameLength = name.Length + 1 + normalizedVersion.Length;
            char[] rentedBuffer = null;
            Span<char> propertyName = propertyNameLength <= StackallocCharThreshold
                ? stackalloc char[propertyNameLength]
                : (rentedBuffer = ArrayPool<char>.Shared.Rent(propertyNameLength));

            try
            {
                name.AsSpan().CopyTo(propertyName);
                propertyName[name.Length] = '/';
                normalizedVersion.AsSpan().CopyTo(propertyName.Slice(name.Length + 1));
                writer.WritePropertyName(propertyName.Slice(0, propertyNameLength));
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    ArrayPool<char>.Shared.Return(rentedBuffer);
                }
            }
        }

        private static void WriteArray(Utf8JsonWriter writer, IEnumerable<string> values)
        {
            writer.WriteStartArray();

            foreach (string value in values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        private static void WriteSortedArray(
            Utf8JsonWriter writer,
            JsonEncodedText propertyName,
            IList<string> values)
        {
            if (values.Count == 0)
            {
                return;
            }

            writer.WritePropertyName(propertyName);
            writer.WriteStartArray();

            WriteOrdinallySorted(
                writer,
                values,
                static value => value,
                static (jsonWriter, value) => jsonWriter.WriteStringValue(value));

            writer.WriteEndArray();
        }

        private static void WriteOrdinallySorted<T>(
            Utf8JsonWriter writer,
            IList<T> values,
            Func<T, string> getSortKey,
            Action<Utf8JsonWriter, T> writeValue)
        {
            if (values.Count == 1)
            {
                writeValue(writer, values[0]);
            }
            else if (values.Count > 1)
            {
                foreach (T value in values.OrderBy(getSortKey, StringComparer.Ordinal))
                {
                    writeValue(writer, value);
                }
            }
        }

        private static void WriteOrdinallySorted<TKey, TValue>(
            Utf8JsonWriter writer,
            IDictionary<TKey, TValue> values,
            Func<KeyValuePair<TKey, TValue>, string> getSortKey,
            Action<Utf8JsonWriter, KeyValuePair<TKey, TValue>> writeValue)
        {
            if (values.Count == 1)
            {
                foreach (KeyValuePair<TKey, TValue> value in values.NoAllocEnumerate())
                {
                    writeValue(writer, value);
                }
            }
            else if (values.Count > 1)
            {
                foreach (KeyValuePair<TKey, TValue> value in values.OrderBy(getSortKey, StringComparer.Ordinal))
                {
                    writeValue(writer, value);
                }
            }
        }

        private static void WriteObject<T>(
            Utf8JsonWriter writer,
            IEnumerable<T> items,
            Action<Utf8JsonWriter, T> writeItem)
        {
            writer.WriteStartObject();

            foreach (T item in items)
            {
                writeItem(writer, item);
            }

            writer.WriteEndObject();
        }

        private static void WriteLockFile(JsonWriter writer, IObjectWriter jsonObjectWriter, LockFile lockFile)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(VersionProperty);
            writer.WriteValue(lockFile.Version);

            writer.WritePropertyName(TargetsProperty);
            JsonUtility.WriteObject(writer, lockFile.Targets, WriteTarget);

            writer.WritePropertyName(LibrariesProperty);
            JsonUtility.WriteObject(writer, lockFile.Libraries, WriteLibrary);

            writer.WritePropertyName(ProjectFileDependencyGroupsProperty);
            JsonUtility.WriteObject(writer, lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup);

            if (lockFile.PackageFolders?.Any() == true)
            {
                writer.WritePropertyName(PackageFoldersProperty);
                JsonUtility.WriteObject(writer, lockFile.PackageFolders, WriteFileItem);
            }

            if (lockFile.Version >= 2)
            {
                if (lockFile.PackageSpec != null)
                {
                    writer.WritePropertyName(PackageSpecProperty);

                    jsonObjectWriter.WriteObjectStart();

                    PackageSpecWriter.Write(lockFile.PackageSpec, jsonObjectWriter, hashing: false, EnvironmentVariableWrapper.Instance, useLegacyWriter: lockFile.Version <= LegacyVersion);

                    jsonObjectWriter.WriteObjectEnd();
                }
            }

            if (lockFile.Version >= 3)
            {
                if (lockFile.LogMessages.Count > 0)
                {
                    var projectPath = lockFile.PackageSpec?.RestoreMetadata?.ProjectPath;
                    writer.WritePropertyName(LogsProperty);
                    WriteLogMessages(writer, lockFile.LogMessages, projectPath);
                }
            }

            if (lockFile.CentralTransitiveDependencyGroups.Any())
            {
                writer.WritePropertyName(CentralTransitiveDependencyGroupsProperty);
                WriteCentralTransitiveDependencyGroup(jsonObjectWriter, lockFile.CentralTransitiveDependencyGroups);
            }

            writer.WriteEndObject();
        }

        private static void WriteLibrary(JsonWriter writer, LockFileLibrary library)
        {
            writer.WritePropertyName(library.Name + "/" + library.Version.ToNormalizedString());

            writer.WriteStartObject();

            if (library.IsServiceable)
            {
                writer.WritePropertyName(ServicableProperty);
                writer.WriteValue(library.IsServiceable);
            }

            if (library.Sha512 != null)
            {
                writer.WritePropertyName(Sha512Property);
                writer.WriteValue(library.Sha512);
            }

            writer.WritePropertyName(TypeProperty);
            writer.WriteValue(library.Type);

            if (library.Path != null)
            {
                writer.WritePropertyName(PathProperty);
                writer.WriteValue(library.Path);
            }

            if (library.MSBuildProject != null)
            {
                writer.WritePropertyName(MSBuildProjectProperty);
                writer.WriteValue(library.MSBuildProject);
            }

            if (library.HasTools)
            {
                writer.WritePropertyName(HasToolsProperty);
                writer.WriteValue(library.HasTools);
            }

            WritePathArray(writer, FilesProperty, library.Files);

            writer.WriteEndObject();
        }

        private static void WriteTarget(JsonWriter writer, LockFileTarget target)
        {
            var key = target.Name;

            writer.WritePropertyName(key);

            JsonUtility.WriteObject(writer, target.Libraries, WriteTargetLibrary);
        }

        /// <summary>
        /// Writes the <see cref="IAssetsLogMessage"/> object to the <see cref="JsonWriter"/>.
        /// </summary>
        /// <param name="logMessage"><code>IAssetsLogMessage</code> representing the log message.</param>
        private static void WriteLogMessage(JsonWriter writer, IAssetsLogMessage logMessage, string projectPath)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(LogMessageProperties.CODE);
            writer.WriteValue(Enum.GetName(typeof(NuGetLogCode), logMessage.Code));

            writer.WritePropertyName(LogMessageProperties.LEVEL);
            writer.WriteValue(Enum.GetName(typeof(LogLevel), logMessage.Level));

            if (logMessage.Level == LogLevel.Warning)
            {
                writer.WritePropertyName(LogMessageProperties.WARNING_LEVEL);
                writer.WriteValue((int)logMessage.WarningLevel);
            }

            if (logMessage.FilePath != null &&
               (projectPath == null || !PathUtility.GetStringComparerBasedOnOS().Equals(logMessage.FilePath, projectPath)))
            {
                // Do not write the file path if it is the same as the project path.
                // This prevents duplicate information in the lock file.
                writer.WritePropertyName(LogMessageProperties.FILE_PATH);
                writer.WriteValue(logMessage.FilePath);
            }

            if (logMessage.StartLineNumber > 0)
            {
                writer.WritePropertyName(LogMessageProperties.START_LINE_NUMBER);
                writer.WriteValue(logMessage.StartLineNumber);
            }

            if (logMessage.StartColumnNumber > 0)
            {
                writer.WritePropertyName(LogMessageProperties.START_COLUMN_NUMBER);
                writer.WriteValue(logMessage.StartColumnNumber);
            }

            if (logMessage.EndLineNumber > 0)
            {
                writer.WritePropertyName(LogMessageProperties.END_LINE_NUMBER);
                writer.WriteValue(logMessage.EndLineNumber);
            }

            if (logMessage.EndColumnNumber > 0)
            {
                writer.WritePropertyName(LogMessageProperties.END_COLUMN_NUMBER);
                writer.WriteValue(logMessage.EndColumnNumber);
            }

            if (logMessage.Message != null)
            {
                writer.WritePropertyName(LogMessageProperties.MESSAGE);
                writer.WriteValue(logMessage.Message);
            }

            if (logMessage.LibraryId != null)
            {
                writer.WritePropertyName(LogMessageProperties.LIBRARY_ID);
                writer.WriteValue(logMessage.LibraryId);
            }

            if (logMessage.TargetGraphs != null &&
                logMessage.TargetGraphs.Any() &&
                logMessage.TargetGraphs.All(l => !string.IsNullOrEmpty(l)))
            {
                writer.WritePropertyName(LogMessageProperties.TARGET_GRAPHS);
                WriteArray(writer, logMessage.TargetGraphs);
            }

            writer.WriteEndObject();
        }

        internal static void WriteLogMessages(JsonWriter writer, IEnumerable<IAssetsLogMessage> logMessages, string projectPath)
        {
            writer.WriteStartArray();

            foreach (var logMessage in logMessages)
            {
                WriteLogMessage(writer, logMessage, projectPath);
            }

            writer.WriteEndArray();
        }

        private static void WriteTargetLibrary(JsonWriter writer, LockFileTargetLibrary library)
        {
            writer.WritePropertyName(library.Name + "/" + library.Version.ToNormalizedString());

            writer.WriteStartObject();

            if (library.Type != null)
            {
                writer.WritePropertyName(TypeProperty);
                writer.WriteValue(library.Type);
            }

            if (library.Framework != null)
            {
                writer.WritePropertyName(FrameworkProperty);
                writer.WriteValue(library.Framework);
            }

            if (library.Dependencies.Count > 0)
            {
                var ordered = library.Dependencies.OrderBy(dependency => dependency.Id, StringComparer.Ordinal);

                writer.WritePropertyName(DependenciesProperty);
                JsonUtility.WriteObject(writer, ordered, JsonUtility.WritePackageDependencyWithLegacyString);
            }

            if (library.FrameworkAssemblies.Count > 0)
            {
                var ordered = library.FrameworkAssemblies.OrderBy(assembly => assembly, StringComparer.Ordinal);

                writer.WritePropertyName(FrameworkAssembliesProperty);
                WriteArray(writer, ordered);
            }

            if (library.CompileTimeAssemblies.Count > 0)
            {
                var ordered = library.CompileTimeAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(CompileProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.AnalyzerAssets.Count > 0)
            {
                var ordered = library.AnalyzerAssets.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(AnalyzersProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.RuntimeAssemblies.Count > 0)
            {
                var ordered = library.RuntimeAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(RuntimeProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.FrameworkReferences.Count > 0)
            {
                var ordered = library.FrameworkReferences.OrderBy(reference => reference, StringComparer.Ordinal);

                writer.WritePropertyName(FrameworkReferencesProperty);
                WriteArray(writer, ordered);
            }

            if (library.ResourceAssemblies.Count > 0)
            {
                var ordered = library.ResourceAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(ResourceProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.NativeLibraries.Count > 0)
            {
                var ordered = library.NativeLibraries.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(NativeProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.ContentFiles.Count > 0)
            {
                var ordered = library.ContentFiles.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(ContentFilesProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.Build.Count > 0)
            {
                var ordered = library.Build.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(BuildProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.BuildMultiTargeting.Count > 0)
            {
                var ordered = library.BuildMultiTargeting.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(BuildMultiTargetingProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.RuntimeTargets.Count > 0)
            {
                var ordered = library.RuntimeTargets.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(RuntimeTargetsProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.ToolsAssemblies.Count > 0)
            {
                var ordered = library.ToolsAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(ToolsProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            if (library.EmbedAssemblies.Count > 0)
            {
                var ordered = library.EmbedAssemblies.OrderBy(assembly => assembly.Path, StringComparer.Ordinal);

                writer.WritePropertyName(EmbedProperty);
                JsonUtility.WriteObject(writer, ordered, WriteFileItem);
            }

            writer.WriteEndObject();
        }

        private static void WriteProjectFileDependencyGroup(JsonWriter writer, ProjectFileDependencyGroup frameworkInfo)
        {
            writer.WritePropertyName(frameworkInfo.FrameworkName);
            WriteArray(writer, frameworkInfo.Dependencies);
        }

        private static void WriteFileItem(JsonWriter writer, LockFileItem item)
        {
            writer.WritePropertyName(item.Path);

            writer.WriteStartObject();

            foreach (var property in item.Properties.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Key);

                if (bool.TrueString.Equals(property.Value, StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteValue(true);
                }
                else if (bool.FalseString.Equals(property.Value, StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteValue(false);
                }
                else
                {
                    writer.WriteValue(property.Value);
                }
            }

            writer.WriteEndObject();
        }

        private static void WritePathArray(JsonWriter writer, string property, IEnumerable<string> items)
        {
            using var itemsEnumerator = items.NoAllocEnumerate().GetEnumerator();
            if (itemsEnumerator.MoveNext())
            {
                var orderedItems = items
                    .Select(f => GetPathWithForwardSlashes(f))
                    .OrderBy(f => f, StringComparer.Ordinal);

                writer.WritePropertyName(property);
                WriteArray(writer, orderedItems);
            }
        }

        internal static void WriteArray(JsonWriter writer, IEnumerable<string> values)
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                writer.WriteValue(value);
            }
            writer.WriteEndArray();
        }

        private static string GetPathWithForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void WriteCentralTransitiveDependencyGroup(IObjectWriter writer, IList<CentralTransitiveDependencyGroup> centralTransitiveDependencyGroups)
        {
            writer.WriteObjectStart();

            foreach (var centralTransitiveDepGroup in centralTransitiveDependencyGroups.OrderBy(ptdg => ptdg.FrameworkName))
            {
                PackageSpecWriter.SetCentralTransitveDependencyGroup(writer, centralTransitiveDepGroup.FrameworkName, centralTransitiveDepGroup.TransitiveDependencies);
            }

            writer.WriteObjectEnd();
        }

    }
}

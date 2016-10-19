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

namespace NuGet.ProjectModel
{
    /// <summary>
    /// RestoreCommand creates DotnetCliToolFile objects when restoring a tool.
    /// These are written to the project's obj folder and consumed by the CLI.
    /// </summary>
    public class DotnetCliToolFile
    {
        /// <summary>
        /// File json.
        /// </summary>
        internal JObject Json { get; }

        /// <summary>
        /// File version.
        /// </summary>
        public int FormatVersion { get; set; } = 1;

        /// <summary>
        /// True if all packages were restored.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Tool id.
        /// </summary>
        public string ToolId { get; set; }

        /// <summary>
        /// Resolved tool version.
        /// </summary>
        public NuGetVersion ToolVersion { get; set; }

        /// <summary>
        /// Requested dependency range.
        /// </summary>
        public VersionRange DependencyRange { get; set;}

        /// <summary>
        /// Package folder and package fallback folders.
        /// These are ordered by precedence.
        /// </summary>
        public IList<string> PackageFolders { get; set; } = new List<string>();

        /// <summary>
        /// Framework -> Lib folder path
        /// </summary>
        public IDictionary<NuGetFramework, IList<string>> DepsFiles { get; set; } = new Dictionary<NuGetFramework, IList<string>>();

        /// <summary>
        /// Restore errors and warnings.
        /// </summary>
        public IList<FileLogEntry> Log { get; set; } = new List<FileLogEntry>();

        public DotnetCliToolFile(JObject json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            ParseJson(json);

            Json = json;
        }

        public DotnetCliToolFile()
        {
            Json = new JObject();
        }

        public static DotnetCliToolFile Load(string path)
        {
            var json = ReadJson(path);

            return Load(json);
        }

        public static DotnetCliToolFile Load(JObject json)
        {
            return new DotnetCliToolFile(json);
        }

        public void Save(string path)
        {
            var json = GetJson(spec: this);

            using (var fileStream = new FileStream(path, FileMode.Create))
            using (var textWriter = new StreamWriter(fileStream))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                json.WriteTo(jsonWriter);
            }
        }

        public static JObject GetJson(DotnetCliToolFile spec)
        {
            var json = new JObject();

            json.Add("formatVersion", spec.FormatVersion);
            json.Add("success", spec.Success);
            json.Add("toolId", spec.ToolId);
            json.Add("toolVersion", spec.ToolVersion.ToNormalizedString());
            json.Add("dependencyRange", spec.DependencyRange.ToNormalizedString());

            var targetsObj = new JObject();
            json.Add("depsFiles", targetsObj);

            foreach (var pair in spec.DepsFiles.OrderBy(e => e.Key.ToString(), StringComparer.Ordinal))
            {
                var targetObj = new JObject();
                targetsObj.Add(pair.Key.ToString(), targetObj);

                foreach (var entry in pair.Value.OrderBy(e => e, StringComparer.Ordinal))
                {
                    targetObj.Add(entry, new JObject());
                }
            }

            var packageFoldersObj = new JObject();
            json.Add("packageFolders", packageFoldersObj);

            foreach (var folder in spec.PackageFolders)
            {
                packageFoldersObj.Add(folder, new JObject());
            }

            var logArray = new JArray();
            json.Add("log", logArray);

            foreach (var entry in spec.Log)
            {
                var entryObj = new JObject();
                logArray.Add(entryObj);

                entryObj.Add("type", entry.Type.ToString().ToString().ToLowerInvariant());
                entryObj.Add("message", entry.Message);
            }

            return json;
        }

        private void ParseJson(JObject json)
        {
            FormatVersion = json.GetValue<int>("formatVersion");
            Success = json.GetValue<bool>("success");
            ToolId = json.GetValue<string>("toolId");
            ToolVersion = GetItem(json, "toolVersion", NuGetVersion.Parse);
            DependencyRange = GetItem(json, "dependencyRange", VersionRange.Parse);

            foreach (var prop in GetProperties(json, "depsFiles"))
            {
                var framework = NuGetFramework.Parse(prop.Name);

                if (!DepsFiles.ContainsKey(framework)
                    && framework.IsSpecificFramework)
                {
                    var files = prop.Value.ToObject<JObject>()?.Properties().Select(e => e.Name)
                        ?? Enumerable.Empty<string>();

                    DepsFiles.Add(framework, files.ToList());
                }
            }

            foreach (var prop in GetProperties(json, "packageFolders"))
            {
                PackageFolders.Add(prop.Name);
            }

            var logArray = json.GetValue<JArray>("log");
            if (logArray != null)
            {
                foreach (var entry in logArray)
                {
                    FileLogEntryType entryType;
                    var typeString = entry.GetValue<string>("type");
                    Enum.TryParse(typeString, ignoreCase: true, result: out entryType);

                    Log.Add(new FileLogEntry(entryType, entry.GetValue<string>("message")));
                }
            }
        }

        private static JObject ReadJson(string packageSpecPath)
        {
            JObject json;

            using (var stream = new FileStream(packageSpecPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                try
                {
                    json = JObject.Load(reader);
                }
                catch (JsonReaderException ex)
                {
                    throw FileFormatException.Create(ex, packageSpecPath);
                }
            }

            return json;
        }

        private static T GetItem<T>(JToken token, string propertyName, Func<string, T> convert)
        {
            var obj = token as JObject;
            var value = obj?.GetValue<string>(propertyName);

            if (value != null)
            {
                return convert(value);
            }

            return default(T);
        }

        private static IEnumerable<JProperty> GetProperties(JToken token, string parentName)
        {
            var obj = token as JObject;

            JToken child;
            if (obj.TryGetValue(parentName, out child))
            {
                var childObj = child as JObject;

                if (childObj != null)
                {
                    return childObj.Properties();
                }
            }

            return Enumerable.Empty<JProperty>();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public static class CacheFileFormat
    {
        private const string VersionProperty = "version";
        private const string DGSpecHashProperty = "dgSpecHash";
        private const string SuccessProperty = "success";
        private const string ExpectedPackageFilesProperty = "expectedPackageFiles";
        private const string ProjectFilePathProperty = "projectFilePath";

        public static CacheFile Read(Stream stream, ILogger log, string path)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (path == null) throw new ArgumentNullException(nameof(path));

            using (var textReader = new StreamReader(stream))
            {
                return Read(textReader, log, path);
            }
        }

        private static CacheFile Read(TextReader reader, ILogger log, string path)
        {
            try
            {
                string jsonString = reader.ReadToEnd();
                var json = JsonDocument.Parse(jsonString);
                var cacheFile = ReadCacheFile(json.RootElement);
                return cacheFile;
            }
            catch (Exception ex)
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.Log_ProblemReadingCacheFile,
                    path, ex.Message));

                // Parsing error, the cache file is invalid. 
                return new CacheFile(null);
            }
        }

        public static void Write(string filePath, CacheFile lockFile)
        {
            // Create the directory if it does not exist
            var fileInfo = new FileInfo(filePath);
            fileInfo.Directory.Create();

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, lockFile);
            }
        }

        public static void Write(Stream stream, CacheFile cacheFile)
        {
#if NET5_0_OR_GREATER
            using (var textWriter = new StreamWriter(stream))
#else
            using (var textWriter = new NoAllocNewLineStreamWriter(stream))
#endif
            {
                Write(textWriter, cacheFile);
            }
        }

        private static void Write(TextWriter textWriter, CacheFile cacheFile)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(GetCacheFile(cacheFile), options);
            textWriter.Write(jsonString);
        }

        private static CacheFile ReadCacheFile(JsonElement cursor)
        {
            var version = cursor.GetProperty(VersionProperty).GetInt32();
            var hash = cursor.GetProperty(DGSpecHashProperty).GetString();
            var success = cursor.GetProperty(SuccessProperty).GetBoolean();
            var cacheFile = new CacheFile(hash)
            {
                Version = version,
                Success = success
            };

            if (version >= 2)
            {
                cacheFile.ProjectFilePath = cursor.GetProperty(ProjectFilePathProperty).GetString();
                cacheFile.ExpectedPackageFilePaths = new List<string>();
                foreach (JsonElement expectedFile in cursor.GetProperty(ExpectedPackageFilesProperty).EnumerateArray())
                {
                    string path = expectedFile.GetString();

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        cacheFile.ExpectedPackageFilePaths.Add(path);
                    }
                }

                cacheFile.LogMessages = LockFileFormat.ReadLogMessageArray(cursor.GetProperty(LockFileFormat.LogsProperty), cacheFile.ProjectFilePath);
            }

            return cacheFile;
        }

        private static object GetCacheFile(CacheFile cacheFile)
        {
            var json = new Dictionary<string, object>
            {
                [VersionProperty] = cacheFile.Version,
                [DGSpecHashProperty] = cacheFile.DgSpecHash,
                [SuccessProperty] = cacheFile.Success
            };

            if (cacheFile.Version >= 2)
            {
                json[ProjectFilePathProperty] = cacheFile.ProjectFilePath;
                json[ExpectedPackageFilesProperty] = cacheFile.ExpectedPackageFilePaths;
                json[LockFileFormat.LogsProperty] = cacheFile.LogMessages == null ? new List<object>() : LockFileFormat.WriteLogMessages(cacheFile.LogMessages, cacheFile.ProjectFilePath);
            }

            return json;
        }
    }
}

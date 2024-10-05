// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public static class CacheFileFormat
    {
        private static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new AssetsLogMessageConverter() }
        };

        /// <summary>
        /// since Log messages property in CacheFile is an interface type, we have the following custom converter to deserialize the IAssetsLogMessage objects.
        /// </summary>
        private class AssetsLogMessageConverter : JsonConverter<IAssetsLogMessage>
        {
            public override IAssetsLogMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                AssetsLogMessage assetsLogMessage = null;

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    using (JsonDocument document = JsonDocument.ParseValue(ref reader))
                    {
                        JsonElement json = document.RootElement;
                        var level = json.GetProperty(LogMessageProperties.LEVEL).GetString();
                        var code = json.GetProperty(LogMessageProperties.CODE).GetString();
                        var message = json.GetProperty(LogMessageProperties.MESSAGE).GetString();

                        if (Enum.TryParse(level, out LogLevel logLevel) && Enum.TryParse(code, out NuGetLogCode logCode))
                        {
                            assetsLogMessage = new AssetsLogMessage(logLevel, logCode, message)
                            {
                                TargetGraphs = json.TryGetProperty(LogMessageProperties.TARGET_GRAPHS, out var targetGraphs)
                                    ? targetGraphs.EnumerateArray().Select(x => x.GetString()).ToList()
                                    : new List<string>()
                            };

                            if (logLevel == LogLevel.Warning && json.TryGetProperty(LogMessageProperties.WARNING_LEVEL, out var warningLevel))
                            {
                                assetsLogMessage.WarningLevel = (WarningLevel)Enum.ToObject(typeof(WarningLevel), warningLevel.GetInt32());
                            }

                            assetsLogMessage.ProjectPath = options.GetType().GetProperty("ProjectPath")?.GetValue(options)?.ToString();

                            if (json.TryGetProperty(LogMessageProperties.FILE_PATH, out var filePath))
                            {
                                assetsLogMessage.FilePath = filePath.GetString();
                            }
                            else
                            {
                                assetsLogMessage.FilePath = assetsLogMessage.ProjectPath;
                            }

                            if (json.TryGetProperty(LogMessageProperties.START_LINE_NUMBER, out var startLineNumber))
                            {
                                assetsLogMessage.StartLineNumber = startLineNumber.GetInt32();
                            }

                            if (json.TryGetProperty(LogMessageProperties.START_COLUMN_NUMBER, out var startColumnNumber))
                            {
                                assetsLogMessage.StartColumnNumber = startColumnNumber.GetInt32();
                            }

                            if (json.TryGetProperty(LogMessageProperties.END_LINE_NUMBER, out var endLineNumber))
                            {
                                assetsLogMessage.EndLineNumber = endLineNumber.GetInt32();
                            }

                            if (json.TryGetProperty(LogMessageProperties.END_COLUMN_NUMBER, out var endColumnNumber))
                            {
                                assetsLogMessage.EndColumnNumber = endColumnNumber.GetInt32();
                            }

                            if (json.TryGetProperty(LogMessageProperties.LIBRARY_ID, out var libraryId))
                            {
                                assetsLogMessage.LibraryId = libraryId.GetString();
                            }
                        }
                    }
                }

                return assetsLogMessage;
            }

            public override void Write(Utf8JsonWriter writer, IAssetsLogMessage value, JsonSerializerOptions options)
            {
                var logJson = new Dictionary<string, object>
                {
                    [LogMessageProperties.CODE] = value.Code.ToString(),
                    [LogMessageProperties.LEVEL] = value.Level.ToString(),
                    [LogMessageProperties.MESSAGE] = value.Message
                };

                if (value.Level == LogLevel.Warning)
                {
                    logJson[LogMessageProperties.WARNING_LEVEL] = value.WarningLevel;
                }

                if (!string.IsNullOrEmpty(value.FilePath) &&
                    (value.ProjectPath == null || !PathUtility.GetStringComparerBasedOnOS().Equals(value.FilePath, value.ProjectPath)))
                {
                    logJson[LogMessageProperties.FILE_PATH] = value.FilePath;
                }

                if (value.StartLineNumber > 0)
                {
                    logJson[LogMessageProperties.START_LINE_NUMBER] = value.StartLineNumber;
                }

                if (value.StartColumnNumber > 0)
                {
                    logJson[LogMessageProperties.START_COLUMN_NUMBER] = value.StartColumnNumber;
                }

                if (value.EndLineNumber > 0)
                {
                    logJson[LogMessageProperties.END_LINE_NUMBER] = value.EndLineNumber;
                }

                if (value.EndColumnNumber > 0)
                {
                    logJson[LogMessageProperties.END_COLUMN_NUMBER] = value.EndColumnNumber;
                }

                if (!string.IsNullOrEmpty(value.LibraryId))
                {
                    logJson[LogMessageProperties.LIBRARY_ID] = value.LibraryId;
                }

                if (value.TargetGraphs != null && value.TargetGraphs.Any() && value.TargetGraphs.All(l => !string.IsNullOrEmpty(l)))
                {
                    logJson[LogMessageProperties.TARGET_GRAPHS] = value.TargetGraphs;
                }

                JsonSerializer.Serialize(writer, logJson, options);
            }
        }

        public static CacheFile Read(Stream stream, ILogger log, string path)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (path == null) throw new ArgumentNullException(nameof(path));

            try
            {
                var cacheFile = JsonSerializer.DeserializeAsync<CacheFile>(utf8Json: stream, SerializerOptions).GetAwaiter().GetResult();
                return cacheFile;
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is JsonException || ex is NotSupportedException)
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.Log_ProblemReadingCacheFile,
                    path, ex.Message));
            }

            return new CacheFile(null);
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
            string jsonString = JsonSerializer.Serialize(cacheFile, SerializerOptions);
            textWriter.Write(jsonString);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Represents arguments to the out-of-proc static graph-based restore which can be written to disk by <see cref="RestoreTaskEx" /> and then read by NuGet.Build.Tasks.Console.
    /// </summary>
    internal class StaticGraphRestoreArguments
    {
        /// <summary>
        /// Gets or sets the path to the entry project.
        /// </summary>
        public string EntryProjectFilePath { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="IEnumerable{T}" /> containing <see cref="KeyValuePair{TKey, TValue}" /> representing the global properties.
        /// </summary>
        public Dictionary<string, string> GlobalProperties { get; set; }

        /// <summary>
        /// Gets or sets the full path to the MSBuild executable.
        /// </summary>
        public string MSBuildExeFilePath { get; set; }

        /// <summary>
        /// Gets or sets an <see cref="IReadOnlyDictionary{TKey, TValue}" /> containing option names and values.
        /// </summary>
        public IReadOnlyDictionary<string, string> Options { get; set; }

        /// <summary>
        /// Reads arguments by searching the specified command-line parameters for an argument file path.
        /// </summary>
        /// <param name="args">An array of <see cref="string" /> objects containing the command-line arguments for the current executable.</param>
        /// <returns>A <see cref="StaticGraphRestoreArguments" /> object if an argument file was found, otherwise <c>null</c>.</returns>
        public static StaticGraphRestoreArguments Read(string[] args)
        {
            // Look for the first command-line argument that starts with '@'
            string argumentFilePath = args.FirstOrDefault(i => i.StartsWith("@", StringComparison.Ordinal))?.Trim('@');

            if (string.IsNullOrWhiteSpace(argumentFilePath))
            {
                return null;
            }

            FileInfo argumentFile = new FileInfo(argumentFilePath);

            if (!argumentFile.Exists)
            {
                return null;
            }

            return Read(argumentFile.FullName);
        }

        /// <summary>
        /// Reads arguments from the specified file path.
        /// </summary>
        /// <param name="argumentFilePath">The full path to the argument file to read.</param>
        /// <returns>A <see cref="StaticGraphRestoreArguments" /> object if the specified argument file was valid, otherwise <c>null</c>.</returns>
        public static StaticGraphRestoreArguments Read(string argumentFilePath)
        {
            using FileStream stream = File.OpenRead(argumentFilePath);

            return Read(stream);
        }

        /// <summary>
        /// Writes the current arguments to the specified file.
        /// </summary>
        /// <param name="argumentFilePath">The full path to the arguments file to write. if the file exists, it is overwritten. If the directory does not exist, it will be created.</param>
        public void Write(string argumentFilePath)
        {
            var fileInfo = new FileInfo(argumentFilePath);

            fileInfo.Directory.Create();

            using FileStream stream = File.OpenWrite(argumentFilePath);

            Write(stream);
        }

        /// <summary>
        /// Reads arguments from the specified <see cref="Stream" />.
        /// </summary>
        /// <param name="stream">The <see cref="Stream" /> containing the contents of an argument file.</param>
        /// <returns>A <see cref="StaticGraphRestoreArguments" /> object if the specified stream contained a valid argument file, otherwise <c>null</c>.</returns>
        internal static StaticGraphRestoreArguments Read(Stream stream)
        {
            using var textReader = new StreamReader(stream);
            using var reader = new JsonTextReader(textReader);

            while (reader.Read() && reader.TokenType != JsonToken.StartObject)
            {
                // Find the first StartObject
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                return null;
            }

            var arguments = new StaticGraphRestoreArguments();

            while (reader.Read())
            {
                // Read to each property and then read the property's contents
                if (reader.TokenType == JsonToken.PropertyName && reader.Value is string propertyName)
                {
                    if (string.Equals(propertyName, nameof(GlobalProperties), StringComparison.Ordinal))
                    {
                        arguments.GlobalProperties = ReadDictionary(reader);
                    }
                    else if (string.Equals(propertyName, nameof(MSBuildExeFilePath), StringComparison.Ordinal))
                    {
                        arguments.MSBuildExeFilePath = ReadValue(reader);
                    }
                    else if (string.Equals(propertyName, nameof(Options), StringComparison.Ordinal))
                    {
                        arguments.Options = ReadDictionary(reader);
                    }
                    else if (string.Equals(propertyName, nameof(EntryProjectFilePath), StringComparison.Ordinal))
                    {
                        arguments.EntryProjectFilePath = ReadValue(reader);
                    }
                }
            }

            return arguments;

            Dictionary<string, string> ReadDictionary(JsonReader reader)
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                {
                    return null;
                }

                Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                while (reader.Read() && reader.TokenType == JsonToken.PropertyName && reader.Value is string key && reader.Read() && reader.TokenType == JsonToken.String && reader.Value is string value)
                {
                    dictionary[key] = value;
                }

                return dictionary;
            }

            string ReadValue(JsonReader reader)
            {
                if (reader.Read() && reader.TokenType == JsonToken.String && reader.Value is string value)
                {
                    return value;
                }

                return null;
            }
        }

        /// <summary>
        /// Writes the current arguments to the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream" /> to write the arguments to.</param>
        internal void Write(FileStream stream)
        {
            using (var streamWriter = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(streamWriter))
            {
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();
                {
                    WriteProperty(writer, nameof(EntryProjectFilePath), EntryProjectFilePath);
                    WriteProperty(writer, nameof(MSBuildExeFilePath), MSBuildExeFilePath);
                    WriteDictionary(writer, nameof(GlobalProperties), GlobalProperties);
                    WriteDictionary(writer, nameof(Options), Options);
                }
                writer.WriteEndObject();
            }

            void WriteDictionary(JsonTextWriter writer, string propertyName, IEnumerable<KeyValuePair<string, string>> dictionary)
            {
                writer.WritePropertyName(propertyName);

                writer.WriteStartObject();
                foreach (KeyValuePair<string, string> option in dictionary)
                {
                    writer.WritePropertyName(option.Key, escape: true);
                    writer.WriteValue(option.Value);
                }
                writer.WriteEndObject();
            }

            void WriteProperty(JsonTextWriter writer, string name, string value)
            {
                writer.WritePropertyName(name);
                writer.WriteValue(value);
            }
        }
    }
}

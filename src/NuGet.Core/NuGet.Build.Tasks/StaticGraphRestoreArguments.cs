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
    public sealed class StaticGraphRestoreArguments
    {
        /// <summary>
        /// Gets or sets a <see cref="Dictionary{TKey, TValue}" /> representing the global properties.
        /// </summary>
#pragma warning disable CA2227 // Collection properties should be read only
        public Dictionary<string, string> GlobalProperties { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        /// <summary>
        /// Gets or sets an <see cref="Dictionary{TKey, TValue}" /> containing option names and values.
        /// </summary>
#pragma warning disable CA2227 // Collection properties should be read only
        public Dictionary<string, string> Options { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        /// <summary>
        /// Reads arguments from the specified <see cref="Stream" />.
        /// </summary>
        /// <param name="reader">A <see cref="TextReader" /> to read arguments from as JSON.</param>
        /// <returns>A <see cref="StaticGraphRestoreArguments" /> object if the specified stream contained a valid argument file, otherwise <c>null</c>.</returns>
        public static StaticGraphRestoreArguments Read(TextReader reader)
        {
            using var jsonTextReader = new JsonTextReader(reader);

            while (jsonTextReader.Read() && jsonTextReader.TokenType != JsonToken.StartObject)
            {
                // Find the first StartObject
            }

            if (jsonTextReader.TokenType != JsonToken.StartObject)
            {
                return null;
            }

            var arguments = new StaticGraphRestoreArguments();

            while (jsonTextReader.Read())
            {
                // Read to each property and then read the property's contents
                if (jsonTextReader.TokenType == JsonToken.PropertyName && jsonTextReader.Value is string propertyName)
                {
                    if (string.Equals(propertyName, nameof(GlobalProperties), StringComparison.Ordinal))
                    {
                        arguments.GlobalProperties = ReadDictionary(jsonTextReader);
                    }
                    else if (string.Equals(propertyName, nameof(Options), StringComparison.Ordinal))
                    {
                        arguments.Options = ReadDictionary(jsonTextReader);
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
        }

        /// <summary>
        /// Writes the current arguments to the specified stream.
        /// </summary>
        /// <param name="writer">The <see cref="TextWriter" /> to write the arguments to.</param>
        public void Write(TextWriter writer)
        {
            using (var jsonTextWriter = new JsonTextWriter(writer))
            {
                jsonTextWriter.Formatting = Formatting.Indented;

                jsonTextWriter.WriteStartObject();
                {
                    WriteDictionary(jsonTextWriter, nameof(GlobalProperties), GlobalProperties);
                    WriteDictionary(jsonTextWriter, nameof(Options), Options);
                }
                jsonTextWriter.WriteEndObject();
            }

            void WriteDictionary(JsonTextWriter writer, string propertyName, Dictionary<string, string> dictionary)
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
        }
    }
}

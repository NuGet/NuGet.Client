// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#pragma warning disable CA2227 // Collection properties should be read only

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

        public Dictionary<string, string> GlobalProperties { get; set; }

        /// <summary>
        /// Gets or sets an <see cref="Dictionary{TKey, TValue}" /> containing option names and values.
        /// </summary>
        public Dictionary<string, string> Options { get; set; }

        /// <summary>
        /// Reads arguments from the specified <see cref="Stream" />.
        /// </summary>
        /// <param name="stream">A <see cref="Stream" /> to read arguments from.</param>
        /// <returns>A <see cref="StaticGraphRestoreArguments" /> object read from the specified stream.</returns>
        public static StaticGraphRestoreArguments Read(Stream stream, Encoding encoding)
        {
            using var reader = new BinaryReader(stream, encoding, leaveOpen: true);

            int count = SkipPreamble();

            return new StaticGraphRestoreArguments
            {
                GlobalProperties = ReadDictionary(count),
                Options = ReadDictionary()
            };

            Dictionary<string, string> ReadDictionary(int count = -1)
            {
                if (count == -1)
                {
                    count = reader.ReadInt32();
                }

                var dictionary = new Dictionary<string, string>(capacity: count, StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < count; i++)
                {
                    dictionary.Add(
                        key: reader.ReadString(),
                        value: reader.ReadString());
                }

                return dictionary;
            }

            int SkipPreamble()
            {
                // Get the preamble from the current encoding which should only be a maximum of 4 bytes
                byte[] preamble = encoding.GetPreamble();

                if (preamble.Length == 0)
                {
                    // Return -1 to the caller if there is no preamble for the encoding meaning that the stream is at the beginning of the expected content
                    return -1;
                }

                // Create a buffer for the preamble which should be a maximum of 4 bytes
                byte[] buffer = new byte[4];

                // Read 
                int readBytes = reader.Read(buffer, 0, buffer.Length);

                int matchingPreambleLength = 0;

                for (int i = 0; i < preamble.Length; i++)
                {
                    if (buffer[i] != preamble[i])
                    {
                        break;
                    }

                    matchingPreambleLength++;
                }

                if (matchingPreambleLength == preamble.Length)
                {
                    int index = matchingPreambleLength;

                    for (int i = 0; i < buffer.Length - matchingPreambleLength; i++)
                    {
                        buffer[i] = buffer[index];
                    }

                    reader.Read(buffer, buffer.Length - matchingPreambleLength, matchingPreambleLength);

                }

                return BitConverter.ToInt32(buffer, 0);
            }
        }

        /// <summary>
        /// Writes the current arguments to the specified stream.
        /// </summary>
        /// <param name="stream">A <see cref="Stream" /> to write the arguments to.</param>
        public void Write(StreamWriter streamWriter)
        {
            using BinaryWriter writer = new BinaryWriter(streamWriter.BaseStream, streamWriter.Encoding, leaveOpen: true);

            WriteDictionary(GlobalProperties);
            WriteDictionary(Options);

            void WriteDictionary(Dictionary<string, string> dictionary)
            {
                writer.Write(dictionary.Count);

                foreach (var item in dictionary)
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }
            }
        }
    }
}

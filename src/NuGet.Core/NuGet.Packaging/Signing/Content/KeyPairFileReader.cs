// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Packaging.Signing
{
    public class KeyPairFileReader : IDisposable
    {
        private readonly StreamReader _reader;

        public KeyPairFileReader(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream.Length > KeyPairFileUtility.MaxSize)
            {
                throw new SignatureException("Manifest file is too large.");
            }

            _reader = new StreamReader(stream, KeyPairFileUtility.Encoding, detectEncodingFromByteOrderMarks: false);
        }

        /// <summary>
        /// Read a section of key value pairs from the file.
        /// Throw for invalid formats.
        /// </summary>
        /// <remarks>Returns an empty set if the file has reached the end.</remarks>
        public Dictionary<string, string> ReadSection()
        {
            var hasEntries = false;
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);

            var line = _reader.ReadLine();
            while (line != null)
            {
                if (line.Length == 0)
                {
                    // End of section
                    if (!hasEntries)
                    {
                        // Empty sections are invalid
                        ThrowInvalidFormat();
                    }

                    break;
                }

                hasEntries = true;
                var pair = GetPair(line);
                var key = pair.Key;

                if (!entries.ContainsKey(key))
                {
                    entries.Add(key, pair.Value);
                }
                else
                {
                    // Key is not allowed or a duplicate
                    ThrowInvalidFormat();
                }

                line = _reader.ReadLine();
            }

            return entries;
        }

        private static KeyValuePair<string, string> GetPair(string line)
        {
            if (line != null)
            {
                var pos = line.IndexOf(':');

                // Verify that : exists
                if (pos > 0)
                {
                    // Verify the key is the expected name.
                    var key = line.Substring(0, pos);
                    var value = line.Substring(pos + 1);
                    return new KeyValuePair<string, string>(key, value);
                }
            }

            // fail if anything is out of place
            throw new SignatureException("Invalid key value pair found in signature files.");
        }

        /// <summary>
        /// True if the reader has reached the end of the file.
        /// </summary>
        public bool EndOfStream
        {
            get
            {
                return _reader.EndOfStream;
            }
        }

        /// <summary>
        /// Fail due to an invalid manifest format.
        /// </summary>
        private static void ThrowInvalidFormat()
        {
            throw new SignatureException("Invalid signing manifest format");
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Packaging.Signing
{
    public class KeyPairFileWriter : IDisposable
    {
        private readonly StreamWriter _writer;

        public KeyPairFileWriter(Stream stream, bool leaveOpen)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _writer = new StreamWriter(stream, KeyPairFileUtility.Encoding, bufferSize: 8192, leaveOpen: leaveOpen);
        }

        /// <summary>
        /// Write key:value with EOL to the manifest stream.
        /// </summary>
        public void WritePair(string key, string value)
        {
            _writer.Write(FormatItem(key, value));
            WriteEOL();
        }

        /// <summary>
        /// Write an empty line.
        /// </summary>
        public void WriteSectionBreak()
        {
            WriteEOL();
        }

        /// <summary>
        /// Write an end of line to the manifest writer.
        /// </summary>
        private void WriteEOL()
        {
            _writer.Write('\n');
        }

        /// <summary>
        /// key:value
        /// </summary>
        private static string FormatItem(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(null, nameof(key));
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(null, nameof(value));
            }

            return $"{key}:{value}";
        }

        public void Dispose()
        {
            _writer.Dispose();
        }
    }
}

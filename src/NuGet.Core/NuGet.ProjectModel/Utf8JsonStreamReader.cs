// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// This struct is used to read over a memeory stream in parts, in order to avoid reading the entire stream into memory.
    /// It functions as a wrapper around <see cref="Utf8JsonStreamReader"/>, while maintaining a stream and a buffer to read from.
    /// </summary>
    internal ref struct Utf8JsonStreamReader
    {
        private static readonly char[] DelimitedStringDelimiters = [' ', ','];
        private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

        private const int BufferSizeDefault = 16 * 1024;
        private const int MinBufferSize = 1024;
        private Utf8JsonReader _reader;
        // The buffer is used to read from the stream in chunks.
        private byte[] _buffer;
        private bool _disposed;
        private ArrayPool<byte> _bufferPool;
        private int _bufferUsed = 0;

        internal Utf8JsonStreamReader(Stream stream, int bufferSize = BufferSizeDefault, ArrayPool<byte> arrayPool = null)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (bufferSize < MinBufferSize)
            {
                throw new ArgumentException($"Buffer size must be at least {MinBufferSize} bytes", nameof(bufferSize));
            }

            _bufferPool = arrayPool ?? ArrayPool<byte>.Shared;
            _buffer = _bufferPool.Rent(bufferSize);
            _disposed = false;
            Stream = stream;
            Stream.Read(_buffer, 0, 3);
            if (!Utf8Bom.AsSpan().SequenceEqual(_buffer.AsSpan(0, 3)))
            {
                _bufferUsed = 3;
            }

            var iniialJsonReaderState = new JsonReaderState(new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            ReadStreamIntoBuffer(iniialJsonReaderState);
            _reader.Read();
        }

        private Stream Stream { get; set; }

        internal bool IsFinalBlock => _reader.IsFinalBlock;

        internal JsonTokenType TokenType => _reader.TokenType;

        internal bool ValueTextEquals(ReadOnlySpan<byte> utf8Text) => _reader.ValueTextEquals(utf8Text);

        internal bool TryGetInt32(out int value) => _reader.TryGetInt32(out value);

        internal string GetString() => _reader.GetString();

        internal bool GetBoolean() => _reader.GetBoolean();

        internal int GetInt32() => _reader.GetInt32();

        internal bool Read()
        {
            ThrowExceptionIfDisposed();

            bool wasRead;
            while (!(wasRead = _reader.Read()) && !_reader.IsFinalBlock)
            {
                GetMoreBytesFromStream();
            }
            return wasRead;
        }

        internal void Skip()
        {
            ThrowExceptionIfDisposed();

            bool wasSkipped;
            while (!(wasSkipped = _reader.TrySkip()) && !_reader.IsFinalBlock)
            {
                GetMoreBytesFromStream();
            }
            if (!wasSkipped)
            {
                _reader.Skip();
            }
        }

        internal string ReadNextTokenAsString()
        {
            ThrowExceptionIfDisposed();

            if (Read())
            {
                return _reader.ReadTokenAsString();
            }

            return null;
        }

        internal IList<string> ReadStringArrayAsIList(IList<string> strings = null)
        {
            if (TokenType == JsonTokenType.StartArray)
            {
                while (Read() && TokenType != JsonTokenType.EndArray)
                {
                    string value = _reader.ReadTokenAsString();

                    strings = strings ?? new List<string>();

                    strings.Add(value);
                }
            }
            return strings;
        }

        internal IReadOnlyList<string> ReadDelimitedString()
        {
            ThrowExceptionIfDisposed();

            if (Read())
            {
                switch (TokenType)
                {
                    case JsonTokenType.String:
                        var value = GetString();

                        return value.Split(DelimitedStringDelimiters, StringSplitOptions.RemoveEmptyEntries);

                    default:
                        var invalidCastException = new InvalidCastException();
                        throw new JsonException(invalidCastException.Message, invalidCastException);
                }
            }

            return null;
        }

        internal bool ReadNextTokenAsBoolOrFalse()
        {
            ThrowExceptionIfDisposed();

            if (Read() && (TokenType == JsonTokenType.False || TokenType == JsonTokenType.True))
            {
                return GetBoolean();
            }
            return false;
        }

        internal IReadOnlyList<string> ReadNextStringOrArrayOfStringsAsReadOnlyList()
        {
            ThrowExceptionIfDisposed();

            if (Read())
            {
                switch (_reader.TokenType)
                {
                    case JsonTokenType.String:
                        return new[] { (string)_reader.GetString() };

                    case JsonTokenType.StartArray:
                        return ReadStringArrayAsReadOnlyListFromArrayStart();

                    case JsonTokenType.StartObject:
                        return null;
                }
            }

            return null;
        }

        internal IReadOnlyList<string> ReadStringArrayAsReadOnlyListFromArrayStart()
        {
            ThrowExceptionIfDisposed();

            List<string> strings = null;

            while (Read() && _reader.TokenType != JsonTokenType.EndArray)
            {
                string value = _reader.ReadTokenAsString();

                strings = strings ?? new List<string>();

                strings.Add(value);
            }

            return (IReadOnlyList<string>)strings ?? Array.Empty<string>();
        }

        // This function is called when Read() returns false and we're not already in the final block
        private void GetMoreBytesFromStream()
        {
            if (_reader.BytesConsumed < _bufferUsed)
            {
                // If the number of bytes consumed by the reader is less than the amount set in the buffer then we have leftover bytes
                var oldBuffer = _buffer;
                ReadOnlySpan<byte> leftover = oldBuffer.AsSpan((int)_reader.BytesConsumed);
                _bufferUsed = leftover.Length;

                // If the leftover bytes are the same as the buffer size then we are at capacity and need to double the buffer size
                if (leftover.Length == _buffer.Length)
                {
                    _buffer = _bufferPool.Rent(_buffer.Length * 2);
                    leftover.CopyTo(_buffer);
                    _bufferPool.Return(oldBuffer, true);
                }
                else
                {
                    leftover.CopyTo(_buffer);
                }
            }
            else
            {
                _bufferUsed = 0;
            }

            ReadStreamIntoBuffer(_reader.CurrentState);
        }

        /// <summary>
        /// Loops through the stream and reads it into the buffer until the buffer is full or the stream is empty
        /// </summary>
        /// <returns>True if the stream is empty</returns>
        private void ReadStreamIntoBuffer(JsonReaderState jsonReaderState)
        {
            int bytesRead;
            do
            {
                var spaceLeftInBuffer = _buffer.Length - _bufferUsed;
                bytesRead = Stream.Read(_buffer, _bufferUsed, spaceLeftInBuffer);
                _bufferUsed += bytesRead;
            }
            while (bytesRead != 0 && _bufferUsed != _buffer.Length);
            _reader = new Utf8JsonReader(_buffer.AsSpan(0, _bufferUsed), isFinalBlock: bytesRead == 0, jsonReaderState);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                byte[] toReturn = _buffer;
                _buffer = null!;
                _bufferPool.Return(toReturn, true);
            }
        }

        private void ThrowExceptionIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Utf8JsonStreamReader));
            }
        }
    }
}

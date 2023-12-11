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
            var offset = 0;
            if (!Utf8Bom.AsSpan().SequenceEqual(_buffer.AsSpan(0, 3)))
            {
                offset = 3;
            }
            var blocksRead = Stream.Read(_buffer, offset, _buffer.Length - offset);

            _reader = new Utf8JsonReader(_buffer.AsSpan(0, blocksRead + offset), isFinalBlock: blocksRead + offset < _buffer.Length, state: new JsonReaderState(new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }));
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

        // This function is called when Read() returns false
        private void GetMoreBytesFromStream()
        {
            int leftoverBytes = 0;
            int bytesReadFromStream;

            if (_reader.BytesConsumed < _buffer.Length)
            {
                // If the number of bytes consumed by the reader is less than the buffer size then we have leftover bytes that need to be shifted
                var oldBuffer = _buffer;
                ReadOnlySpan<byte> leftover = oldBuffer.AsSpan((int)_reader.BytesConsumed);

                var returnOldBuffer = false;

                // If the leftover bytes are the same as the buffer size then we are at capacity and need to double the buffer size
                if (leftover.Length == _buffer.Length)
                {
                    returnOldBuffer = true;
                    _buffer = _bufferPool.Rent(_buffer.Length * 2);
                }

                //Copy the leftover bytes to the beginning of the new buffer
                leftover.CopyTo(_buffer);

                // Read the rest of the bytes from the stream, keeping track of the number of bytes that need to be processed in the new buffer
                leftoverBytes = leftover.Length;
                bytesReadFromStream = Stream.Read(_buffer, leftover.Length, _buffer.Length - leftover.Length);
                if (returnOldBuffer)
                {
                    _bufferPool.Return(oldBuffer, true);
                }
            }
            else
            {
                bytesReadFromStream = Stream.Read(_buffer, 0, _buffer.Length);
            }
            _reader = new Utf8JsonReader(_buffer.AsSpan(0, leftoverBytes + bytesReadFromStream), isFinalBlock: leftoverBytes + bytesReadFromStream < _buffer.Length, _reader.CurrentState);
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

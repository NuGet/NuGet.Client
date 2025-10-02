// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NuGet.PackageManagement.VisualStudio.Migrate
{
    internal ref struct Utf8JsonStreamReader
    {
        private static readonly char[] DelimitedStringDelimiters = [' ', ','];
        private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
        private static readonly JsonReaderOptions DefaultJsonReaderOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        private const int BufferSizeDefault = 16 * 1024;
        private const int MinBufferSize = 1024;
        private Utf8JsonReader _reader;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Stream _stream;
#pragma warning restore CA2213 // Disposable fields should be disposed
        // The buffer is used to read from the stream in chunks.
        private byte[] _buffer;
        private bool _disposed;
        private ArrayPool<byte> _bufferPool;
        private int _bufferUsed = 0;

        internal bool ValueTextEquals(ReadOnlySpan<byte> utf8Text) => _reader.ValueTextEquals(utf8Text);
        internal bool GetBoolean() => _reader.GetBoolean();
        internal string GetString() => _reader.GetString();
        internal JsonTokenType TokenType => _reader.TokenType;

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
            _stream = stream;

            if (_stream.Read(_buffer, offset: 0, count: 1) == 1 &&
                _stream.Read(_buffer, offset: ++_bufferUsed, count: 1) == 1 &&
                _stream.Read(_buffer, offset: ++_bufferUsed, count: 1) == 1)
            {
                ++_bufferUsed;

                bool hasUtf8Bom = Utf8Bom.AsSpan().SequenceEqual(_buffer.AsSpan(start: 0, length: 3));

                if (hasUtf8Bom)
                {
                    _bufferUsed = 0;
                }
            }

            var initialJsonReaderState = new JsonReaderState(DefaultJsonReaderOptions);

            ReadStreamIntoBuffer(initialJsonReaderState);
            _reader.Read();
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
        /// Loops through the stream and reads it into the buffer until the buffer is full or the stream is empty, creates the Utf8JsonReader.
        /// </summary>
        private void ReadStreamIntoBuffer(JsonReaderState jsonReaderState)
        {
            int bytesRead;
            do
            {
                var spaceLeftInBuffer = _buffer.Length - _bufferUsed;
                bytesRead = _stream.Read(_buffer, _bufferUsed, spaceLeftInBuffer);
                _bufferUsed += bytesRead;
            }
            while (bytesRead != 0 && _bufferUsed != _buffer.Length);
            _reader = new Utf8JsonReader(_buffer.AsSpan(0, _bufferUsed), isFinalBlock: bytesRead == 0, jsonReaderState);
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

                    strings ??= new List<string>();

                    strings.Add(value);
                }
            }
            return strings;
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

        internal bool ReadNextTokenAsBoolOrThrowAnException(byte[] propertyName)
        {
            ThrowExceptionIfDisposed();

            if (Read() && (TokenType == JsonTokenType.False || TokenType == JsonTokenType.True))
            {
                return GetBoolean();
            }
            else
            {
                throw new ArgumentException("Invalid attribute", nameof(propertyName));
            }
        }

        internal IReadOnlyList<string> ReadStringArrayAsReadOnlyListFromArrayStart()
        {
            ThrowExceptionIfDisposed();

            List<string> strings = null;

            while (Read() && _reader.TokenType != JsonTokenType.EndArray)
            {
                string value = _reader.ReadTokenAsString();

                strings ??= new List<string>();

                strings.Add(value);
            }

            return (IReadOnlyList<string>)strings ?? Array.Empty<string>();
        }

        internal IReadOnlyList<string> ReadNextStringOrArrayOfStringsAsReadOnlyList()
        {
            ThrowExceptionIfDisposed();

            if (Read())
            {
                switch (_reader.TokenType)
                {
                    case JsonTokenType.String:
                        return new[] { _reader.GetString() };

                    case JsonTokenType.StartArray:
                        return ReadStringArrayAsReadOnlyListFromArrayStart();

                    case JsonTokenType.StartObject:
                        return null;
                }
            }

            return null;
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
                        throw new InvalidCastException();
                }
            }

            return null;
        }

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

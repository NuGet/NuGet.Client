// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// This struct is used to read over a memeory stream in parts, in order to avoid reading the entire stream into memory.
    /// It functions as a wrapper around <see cref="Utf8JsonStreamReader"/>, while maintaining a stream and a buffer to read from.
    /// must call <see cref="CompleteRead"/> to return the buffer to the pool when completed.
    /// </summary>
    internal ref struct Utf8JsonStreamReader
    {
        private static readonly char[] DelimitedStringDelimiters = new char[] { ' ', ',' };

        private Utf8JsonReader _reader;
        // The buffer is used to read from the stream in chunks.
        private byte[] _buffer;
        // The stream is the source of the JSON data.
        private Stream _stream;
        private bool _complete;

        internal Utf8JsonStreamReader(Stream stream) : this(stream, ArrayPool<byte>.Shared.Rent(1024))
        {

        }

        internal Utf8JsonStreamReader(Stream stream, byte[] buffer)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _complete = false;
            _stream = stream;
            _buffer = buffer;
            _stream.Read(_buffer, 0, 3);
            var offset = 0;
            if (!Encoding.UTF8.GetPreamble().AsSpan().SequenceEqual(_buffer.AsSpan(0, 3)))
            {
                offset = 3;
            }
            _stream.Read(_buffer, offset, _buffer.Length - offset);
            _reader = new Utf8JsonReader(_buffer, isFinalBlock: false, state: new JsonReaderState(new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }));
            _reader.Read();
        }

        internal bool IsFinalBlock => _reader.IsFinalBlock;

        internal JsonTokenType TokenType => _reader.TokenType;

        internal int BufferSize()
        {
            ThrowExceptionIfCompleted();

            return _buffer.Length;
        }

        internal bool ValueTextEquals(ReadOnlySpan<byte> utf8Text) => _reader.ValueTextEquals(utf8Text);

        internal bool ValueTextEquals(string text) => _reader.ValueTextEquals(text);

        internal bool TryGetInt32(out int value) => _reader.TryGetInt32(out value);

        internal string GetString() => _reader.GetString();

        internal bool GetBoolean() => _reader.GetBoolean();

        internal int GetInt32() => _reader.GetInt32();

        internal void CompleteRead()
        {
            _complete = true;
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        internal bool Read()
        {
            ThrowExceptionIfCompleted();

            bool wasRead;
            while (!(wasRead = _reader.Read()) && !_reader.IsFinalBlock)
            {
                GetMoreBytesFromStream();
            }
            return wasRead;
        }

        internal bool TrySkip()
        {
            ThrowExceptionIfCompleted();

            bool wasSkipped;
            while (!(wasSkipped = _reader.TrySkip()) && !_reader.IsFinalBlock)
            {
                GetMoreBytesFromStream();
            }
            return wasSkipped;
        }

        internal string ReadNextTokenAsString()
        {
            ThrowExceptionIfCompleted();

            if (Read())
            {
                return _reader.ReadTokenAsString();
            }

            return null;
        }

        internal string GetCurrentBufferAsString()
        {
            ThrowExceptionIfCompleted();

            return Encoding.UTF8.GetString(_buffer);
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

        internal List<string> ReadStringArrayAsList()
        {
            return (List<string>)ReadStringArrayAsIList();
        }

        internal IReadOnlyList<string> ReadDelimitedString()
        {
            ThrowExceptionIfCompleted();

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
            ThrowExceptionIfCompleted();

            if (Read() && (TokenType == JsonTokenType.False || TokenType == JsonTokenType.True))
            {
                return GetBoolean();
            }
            return false;
        }

        internal IReadOnlyList<string> ReadNextStringOrArrayOfStringsAsReadOnlyList()
        {
            ThrowExceptionIfCompleted();

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
            ThrowExceptionIfCompleted();

            List<string> strings = null;

            while (Read() && _reader.TokenType != JsonTokenType.EndArray)
            {
                string value = _reader.ReadTokenAsString();

                strings = strings ?? new List<string>();

                strings.Add(value);
            }

            return (IReadOnlyList<string>)strings ?? Array.Empty<string>();
        }

        private void GetMoreBytesFromStream()
        {
            if (_reader.BytesConsumed < _buffer.Length)
            {
                ReadOnlySpan<byte> leftover = _buffer.AsSpan((int)_reader.BytesConsumed);
                if (leftover.Length == _buffer.Length)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                    _buffer = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
                }
                leftover.CopyTo(_buffer);
                _stream.Read(_buffer, leftover.Length, _buffer.Length - leftover.Length);
            }
            else
            {
                _stream.Read(_buffer, 0, _buffer.Length);
            }
            _reader = new Utf8JsonReader(_buffer, isFinalBlock: _stream.Length == _stream.Position, _reader.CurrentState);
        }

        private void ThrowExceptionIfCompleted()
        {
            if (_complete)
            {
                throw new InvalidOperationException("Cannot read from completed Utf8JsonStreamReader");
            }
        }
    }
}

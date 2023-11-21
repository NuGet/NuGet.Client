// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace NuGet.ProjectModel
{

    internal ref struct StreamingUtf8JsonReader

    {
        private static readonly char[] DelimitedStringDelimiters = new char[] { ' ', ',' };

        private ReadOnlySpan<byte> _utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
        private Utf8JsonReader _reader;
        private byte[] _buffer;
        private Stream _stream;

        internal StreamingUtf8JsonReader(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _stream = stream;
            var firstThreeBytes = new byte[3];
            _stream.Read(firstThreeBytes, 0, 3);
            _buffer = new byte[1024];
            var offset = 0;
            if (!_utf8Bom.SequenceEqual(firstThreeBytes))
            {
                firstThreeBytes.CopyTo(_buffer, 0);
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

        internal int BufferSize => _buffer.Length;

        internal JsonTokenType TokenType => _reader.TokenType;

        internal bool ValueTextEquals(ReadOnlySpan<byte> utf8Text) => _reader.ValueTextEquals(utf8Text);

        internal bool ValueTextEquals(string text) => _reader.ValueTextEquals(text);

        internal bool TryGetInt32(out int value) => _reader.TryGetInt32(out value);

        internal string GetString() => _reader.GetString();

        internal bool GetBoolean() => _reader.GetBoolean();

        internal int GetInt32() => _reader.GetInt32();

        internal bool Read()
        {
            bool wasRead;
            while (!(wasRead = _reader.Read()) && !_reader.IsFinalBlock)
            {
                GetMoreBytesFromStream();
            }
            return wasRead;
        }

        internal bool TrySkip()
        {
            bool wasSkipped;
            while (!(wasSkipped = _reader.TrySkip()) && !_reader.IsFinalBlock)
            {
                GetMoreBytesFromStream();
            }
            return wasSkipped;
        }

        internal IList<T> ReadObjectAsList<T>(JsonSerializerOptions options)
        {
            if (TokenType != JsonTokenType.StartObject)
            {
                return new List<T>(0);

            }
            //We use JsonObjects for the arrays so we advance to the first property in the object which is the name/ver of the first library
            Read();

            if (TokenType == JsonTokenType.EndObject)
            {
                return new List<T>(0);
            }

            var objectConverter = (StreamableJsonConverter<T>)options.GetConverter(typeof(T));
            var listObjects = new List<T>();
            do
            {
                listObjects.Add(objectConverter.ReadWithStream(ref this, options));
                //At this point we're looking at the EndObject token for the object, need to advance.
                Read();
            }
            while (TokenType != JsonTokenType.EndObject);
            return listObjects;
        }

        internal void ReadArrayOfObjects<T1, T2>(JsonSerializerOptions options, IList<T2> objectList) where T1 : T2
        {
            if (objectList is null)
            {
                return;
            }

            var type = typeof(T1);
            var objectConverter = (StreamableJsonConverter<T1>)options.GetConverter(type);

            if (Read() && TokenType == JsonTokenType.StartArray)
            {
                while (Read() && TokenType != JsonTokenType.EndArray)
                {
                    var convertedObject = objectConverter.ReadWithStream(ref this, options);
                    if (convertedObject != null)
                    {
                        objectList.Add(convertedObject);
                    }
                }
            }
        }

        internal string ReadNextTokenAsString()
        {
            if (Read())
            {
                return _reader.ReadTokenAsString();
            }

            return null;
        }

        internal string GetCurrentBufferAsString() => Encoding.UTF8.GetString(_buffer);

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

        internal List<string> ReadStringArrayAsList(List<string> strings = null)
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

        internal bool ReadNextTokenAsBoolOrFalse()
        {
            if (Read() && (TokenType == JsonTokenType.False || TokenType == JsonTokenType.True))
            {
                return GetBoolean();
            }
            return false;
        }

        internal IReadOnlyList<string> ReadNextStringOrArrayOfStringsAsReadOnlyList()
        {
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
                    Array.Resize(ref _buffer, _buffer.Length * 2);
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
    }
}

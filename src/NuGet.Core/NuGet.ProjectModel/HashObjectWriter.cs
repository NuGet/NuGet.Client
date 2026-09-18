// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using NuGet.Packaging;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    internal readonly struct HashObjectWriterAdapter : IObjectWriter
    {
        private readonly HashObjectWriter _writer;

        internal HashObjectWriterAdapter(HashObjectWriter writer)
        {
            _writer = writer;
        }

        public void WriteObjectStart() => _writer.WriteObjectStart();

        public void WriteObjectStart(string name) => _writer.WriteObjectStart(name);

        public void WriteObjectEnd() => _writer.WriteObjectEnd();

        public void WriteNameValue(string name, int value) => _writer.WriteNameValue(name, value);

        public void WriteNameValue(string name, bool value) => _writer.WriteNameValue(name, value);

        public void WriteNameValue(string name, string? value) => _writer.WriteNameValue(name, value);

        public void WriteNameArray(string name, IEnumerable<string> values) => _writer.WriteNameArray(name, values);

        public void WriteNonEmptyNameArray(string name, IEnumerable<string> values) => _writer.WriteNonEmptyNameArray(name, values);

        public void WriteArrayStart(string name) => _writer.WriteArrayStart(name);

        public void WriteArrayEnd() => _writer.WriteArrayEnd();
    }

    /// <summary>
    /// Generates a hash from an object graph.
    ///
    /// This is non-private only to facilitate unit testing.
    /// </summary>
    public sealed class HashObjectWriter : IObjectWriter, IDisposable
    {
        private const int DefaultBufferSize = 4096;

        private readonly byte[] _buffer;
        private readonly IHashFunction _hashFunc;
        private bool _isDisposed;
        private bool _isReadOnly;
        private int _nestLevel;
        private readonly CircularMemoryStream _stream;
        private readonly Utf8JsonWriter _jsonWriter;
        private readonly Utf8JsonObjectWriter _writer;

        /// <summary>
        /// Creates a new instance with the provide hash function.
        /// </summary>
        /// <param name="hashFunc">An <see cref="IHashFunction"/> instance.  Throws if <see langword="null" />.</param>
        public HashObjectWriter(IHashFunction hashFunc)
        {
            if (hashFunc == null)
            {
                throw new ArgumentNullException(nameof(hashFunc));
            }

            _buffer = new byte[DefaultBufferSize];
            _hashFunc = hashFunc;
            _stream = new CircularMemoryStream(_buffer);
            _jsonWriter = new Utf8JsonWriter(
                (IBufferWriter<byte>)_stream,
                new JsonWriterOptions
                {
                    // HashObjectWriter validates the object nesting itself.
                    SkipValidation = true
                });
            _writer = new Utf8JsonObjectWriter(_jsonWriter);

            _stream.OnFlush += OnFlush;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _stream.OnFlush -= OnFlush;

                _hashFunc.Dispose();
                _jsonWriter.Dispose();
                _stream.Dispose();

                _isDisposed = true;
            }
        }

        public void WriteObjectStart()
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteObjectStart();

            ++_nestLevel;
        }

        public void WriteObjectStart(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteObjectStart(name);

            ++_nestLevel;
        }

        public void WriteObjectEnd()
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();

            if (_nestLevel == 0)
            {
                throw new InvalidOperationException();
            }

            _writer.WriteObjectEnd();

            --_nestLevel;
        }

        public void WriteNameValue(string name, int value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteNameValue(name, value);
        }

        public void WriteNameValue(string name, bool value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteNameValue(name, value);
        }

        public void WriteNameValue(string name, string? value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteNameValue(name, value);
        }

        public void WriteNameArray(string name, IEnumerable<string> values)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteNameArray(name, values);
        }

        public void WriteNonEmptyNameArray(string name, IEnumerable<string> values)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteNonEmptyNameArray(name, values);
        }

        /// <summary>
        /// Gets the hash for the object.
        ///
        /// Once GetHash is called, no further writing is allowed.
        /// </summary>
        /// <returns>A hash of the object.</returns>
        public string GetHash()
        {
            ThrowIfDisposed();

            if (!_isReadOnly)
            {
                _jsonWriter.Flush();
                _stream.Flush();

                _isReadOnly = true;
            }

            return _hashFunc.GetHash();
        }

        public void WriteArrayStart(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            ThrowIfDisposed();
            ThrowIfReadOnly();

            _writer.WriteArrayStart(name);

            ++_nestLevel;
        }

        public void WriteArrayEnd()
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();

            if (_nestLevel == 0)
            {
                throw new InvalidOperationException();
            }

            _writer.WriteArrayEnd();

            --_nestLevel;
        }

        private void OnFlush(object? sender, ArraySegment<byte> bytes)
        {
            if (bytes.Count > 0)
            {
                _hashFunc.Update(bytes.Array!, bytes.Offset, bytes.Count);
            }
        }

        private void ThrowIfReadOnly()
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(HashObjectWriter));
            }
        }
    }
}

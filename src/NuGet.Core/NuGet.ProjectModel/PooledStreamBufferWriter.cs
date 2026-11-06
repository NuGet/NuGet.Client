// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.IO;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Incrementally copies pooled UTF-8 output to a stream without taking ownership of the stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stream overload of <see cref="System.Text.Json.Utf8JsonWriter"/> uses a non-pooled <see cref="ArrayBufferWriter{T}"/>
    /// and buffers output until flushed. Flushing only after serialization retains the complete document
    /// and can allocate and copy progressively larger arrays.
    /// </para>
    /// <para>
    /// This adapter rents reusable storage from <see cref="ArrayPool{T}"/> and writes committed chunks as the buffer fills.
    /// This avoids document-sized buffer growth for normal lock-file values.
    /// A large contiguous request can still grow the buffer beyond its initial 16 KiB.
    /// </para>
    /// <para>
    /// Periodically flushing the standard JSON stream writer is an alternative, but also flushes the destination stream
    /// and does not pool its buffer.
    /// </para>
    /// <para>
    /// Dispose returns the rented buffer without flushing, so callers must flush successful writes explicitly.
    /// This avoids writing a failed serialization's buffered tail, but cannot undo bytes already written.
    /// </para>
    /// </remarks>
    internal sealed class PooledStreamBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private const int DefaultBufferSize = 16 * 1024;

        private readonly Stream _stream;
        private byte[]? _buffer;
        private int _written;

        internal PooledStreamBufferWriter(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        }

        public void Advance(int count)
        {
            byte[] buffer = GetBuffer();
            if (count < 0 || count > buffer.Length - _written)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _written += count;
            if (_written == buffer.Length)
            {
                Flush();
            }
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return GetBuffer().AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return GetBuffer().AsSpan(_written);
        }

        internal void Flush()
        {
            byte[] buffer = GetBuffer();
            if (_written == 0)
            {
                return;
            }

            int count = _written;
            _written = 0;

#if NET5_0_OR_GREATER
            _stream.Write(buffer.AsSpan(0, count));
#else
            _stream.Write(buffer, offset: 0, count);
#endif
        }

        public void Dispose()
        {
            byte[]? buffer = _buffer;
            _buffer = null;
            _written = 0;

            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            byte[] buffer = GetBuffer();
            if (sizeHint <= buffer.Length - _written)
            {
                return;
            }

            Flush();
            if (sizeHint > buffer.Length)
            {
                byte[] replacement = ArrayPool<byte>.Shared.Rent(Math.Max(DefaultBufferSize, sizeHint));
                _buffer = replacement;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private byte[] GetBuffer()
        {
            return _buffer ?? throw new ObjectDisposedException(nameof(PooledStreamBufferWriter));
        }
    }
}

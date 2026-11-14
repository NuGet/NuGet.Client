// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.IO;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A circular memory stream backed by a fixed-size byte buffer.
    /// </summary>
    /// <remarks>
    /// <see cref="IBufferWriter{T}" /> allows <see cref="System.Text.Json.Utf8JsonWriter" /> to write directly into
    /// the circular buffer instead of using its slower <see cref="Stream" /> output path. The buffer-writer members
    /// lend writable memory and commit completed bytes through the existing flush logic, preserving deterministic
    /// fixed-size hash updates. When a requested contiguous region is larger than the remaining circular-buffer
    /// space, a temporary pooled buffer satisfies the interface contract and is copied back through the bounded
    /// write path when committed.
    /// </remarks>
    internal sealed class CircularMemoryStream : MemoryStream, IBufferWriter<byte>
    {
        private readonly byte[] _buffer;
        private byte[]? _oversizedBuffer;

        internal event EventHandler<ArraySegment<byte>>? OnFlush;

        internal CircularMemoryStream(byte[] buffer) : base(buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            _buffer = buffer;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var bytesWritten = 0;

            while (bytesWritten < count)
            {
                var bytesToWrite = Math.Min(_buffer.Length - (int)Position, count - bytesWritten);

                base.Write(buffer, offset + bytesWritten, bytesToWrite);

                bytesWritten += bytesToWrite;

                FlushIfFull();
            }
        }

#if NET5_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var bytesWritten = 0;

            while (bytesWritten < buffer.Length)
            {
                var bytesToWrite = Math.Min(_buffer.Length - (int)Position, buffer.Length - bytesWritten);

                base.Write(buffer.Slice(bytesWritten, bytesToWrite));

                bytesWritten += bytesToWrite;

                FlushIfFull();
            }
        }
#endif

        public override void WriteByte(byte value)
        {
            base.WriteByte(value);

            FlushIfFull();
        }

        public void Advance(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (_oversizedBuffer != null)
            {
                if (count > _oversizedBuffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                Write(_oversizedBuffer, offset: 0, count);
                ArrayPool<byte>.Shared.Return(_oversizedBuffer);
                _oversizedBuffer = null;
                return;
            }

            if (count > _buffer.Length - Position)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Position += count;
            FlushIfFull();
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            ValidateSizeHint(sizeHint);

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (sizeHint > _buffer.Length - Position)
            {
                if (_oversizedBuffer == null || _oversizedBuffer.Length < sizeHint)
                {
                    if (_oversizedBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(_oversizedBuffer);
                    }

                    _oversizedBuffer = ArrayPool<byte>.Shared.Rent(sizeHint);
                }

                return _oversizedBuffer;
            }

            return _buffer.AsMemory((int)Position);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return GetMemory(sizeHint).Span;
        }

        public override void Flush()
        {
            base.Flush();

            var handler = OnFlush;

            if (handler != null)
            {
                handler(this, new ArraySegment<byte>(_buffer, offset: 0, count: (int)Position));
            }

            Position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (_oversizedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_oversizedBuffer);
                _oversizedBuffer = null;
            }

            base.Dispose(disposing);
        }

        private void FlushIfFull()
        {
            if (Position == _buffer.Length)
            {
                Flush();
            }
        }

        private static void ValidateSizeHint(int sizeHint)
        {
            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }
        }
    }
}

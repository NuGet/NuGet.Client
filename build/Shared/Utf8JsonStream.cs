// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Shared
{
    /// <summary>
    /// Removes an optional UTF-8 byte order mark from a JSON input stream.
    /// </summary>
    /// <remarks>
    /// JSON readers in NuGet consume UTF-8. This helper centralizes UTF-8 byte order mark
    /// detection and prefix replay for non-seekable streams without buffering the full document.
    /// The caller must dispose the returned stream. The <c>leaveOpen</c> argument to
    /// <see cref="Create"/> controls whether disposal also disposes the input stream.
    /// </remarks>
    internal static class Utf8JsonStream
    {
        private const int Utf8Bom = 0x00BFBBEF;
        private const int Utf8BomLength = 3;

        /// <summary>
        /// Creates a read-only UTF-8 view over <paramref name="stream"/> at its current position.
        /// </summary>
        /// <param name="stream">The JSON stream to normalize.</param>
        /// <param name="leaveOpen">
        /// <see langword="true"/> to leave <paramref name="stream"/> open when the returned stream
        /// is disposed; otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>
        /// A read-only stream that emits BOM-free UTF-8 bytes. The caller must dispose this stream.
        /// </returns>
        internal static Stream Create(Stream stream, bool leaveOpen)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            long initialPosition = stream.CanSeek ? stream.Position : 0;
            int prefix = ReadPrefix(stream, out int prefixLength);
            bool hasUtf8Bom = prefixLength == Utf8BomLength && prefix == Utf8Bom;

            if (stream.CanSeek)
            {
                stream.Position = initialPosition + (hasUtf8Bom ? Utf8BomLength : 0);
                return leaveOpen ? new LeaveOpenReadStream(stream) : stream;
            }

            if (hasUtf8Bom)
            {
                return leaveOpen ? new LeaveOpenReadStream(stream) : stream;
            }

            return new PrefixReplayStream(prefix, prefixLength, stream, leaveOpen);
        }

        private static int ReadPrefix(Stream stream, out int prefixLength)
        {
            int prefix = 0;
            prefixLength = 0;
#if NETFRAMEWORK
            while (prefixLength < Utf8BomLength)
            {
                int value = stream.ReadByte();
                if (value < 0)
                {
                    break;
                }

                prefix |= value << (prefixLength * 8);
                prefixLength++;
            }
#else
            Span<byte> bytes = stackalloc byte[Utf8BomLength];
            while (prefixLength < bytes.Length)
            {
                int bytesRead = stream.Read(bytes.Slice(prefixLength));
                if (bytesRead == 0)
                {
                    break;
                }

                prefixLength += bytesRead;
            }

            for (int i = 0; i < prefixLength; i++)
            {
                prefix |= bytes[i] << (i * 8);
            }
#endif

            return prefix;
        }

        private static void ValidateReadArguments(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
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
            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("Offset and count exceed the buffer length.");
            }
        }

        private sealed class LeaveOpenReadStream : Stream
        {
#pragma warning disable CA2213 // This wrapper intentionally leaves the stream open.
            private readonly Stream _stream;
#pragma warning restore CA2213
            private bool _disposed;

            internal LeaveOpenReadStream(Stream stream)
            {
                _stream = stream;
            }

            public override bool CanRead => !_disposed && _stream.CanRead;
            public override bool CanSeek => !_disposed && _stream.CanSeek;
            public override bool CanWrite => false;
            public override long Length
            {
                get
                {
                    ThrowIfDisposed();
                    return _stream.Length;
                }
            }
            public override long Position
            {
                get
                {
                    ThrowIfDisposed();
                    return _stream.Position;
                }
                set
                {
                    ThrowIfDisposed();
                    _stream.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();
                return _stream.Read(buffer, offset, count);
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                _disposed = true;
                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                ThrowIfDisposed();
                return _stream.Seek(offset, origin);
            }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            private void ThrowIfDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(LeaveOpenReadStream));
                }
            }
        }

        private sealed class PrefixReplayStream : Stream
        {
            private readonly int _prefix;
            private readonly int _prefixLength;
            private readonly Stream _stream;
            private readonly bool _leaveOpen;
            private int _prefixPosition;
            private bool _disposed;

            internal PrefixReplayStream(
                int prefix,
                int prefixLength,
                Stream stream,
                bool leaveOpen)
            {
                _prefix = prefix;
                _prefixLength = prefixLength;
                _stream = stream;
                _leaveOpen = leaveOpen;
            }

            public override bool CanRead => !_disposed && _stream.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();
                ValidateReadArguments(buffer, offset, count);

                int prefixBytesRead = Math.Min(count, _prefixLength - _prefixPosition);
                for (int i = 0; i < prefixBytesRead; i++)
                {
                    buffer[offset + i] = (byte)(_prefix >> ((_prefixPosition + i) * 8));
                }

                if (prefixBytesRead > 0)
                {
                    _prefixPosition += prefixBytesRead;
                }

                int streamBytesRead = prefixBytesRead < count
                    ? _stream.Read(buffer, offset + prefixBytesRead, count - prefixBytesRead)
                    : 0;

                return prefixBytesRead + streamBytesRead;
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    if (disposing && !_leaveOpen)
                    {
                        _stream.Dispose();
                    }
                }

                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            private void ThrowIfDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(PrefixReplayStream));
                }
            }
        }

    }
}
